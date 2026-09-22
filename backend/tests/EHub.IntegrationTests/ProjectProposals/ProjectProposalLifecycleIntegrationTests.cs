using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.ProjectProposals;
using EHub.Application.Features.ProposalAnalyses;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.BackgroundJobs;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EHub.IntegrationTests.ProjectProposals;

[Collection("Sequential")]
public sealed class ProjectProposalLifecycleIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectProposalLifecycleIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedProposalEndpoint_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/workspace/teams/{Guid.NewGuid()}/proposal");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

        var analysisResponse = await client.GetAsync($"/api/workspace/proposal-analyses/{Guid.NewGuid()}");
        analysisResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

        var featuresResponse = await client.GetAsync("/api/features");
        featuresResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnalysisWorker_RespectsFeatureFlag_AndCompletesExactlyOnce()
    {
        Guid jobId;
        Guid leaderUserId;
        Guid teamId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seed = await CreateSeedAsync(context);
            leaderUserId = seed.LeaderUserId;
            teamId = seed.TeamId;
            context.ChangeTracker.Clear();
            var handler = CreateHandler(setupScope, context, aiEnabled: true);
            var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Worker"), seed.LeaderUserId, SystemRoles.Student);
            var submitted = await handler.SubmitAsync(created.Value.Id,
                new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion },
                seed.LeaderUserId,
                SystemRoles.Student);
            submitted.IsSuccess.Should().BeTrue(submitted.Error.Message);
            jobId = submitted.Value.CurrentAnalysisJobId!.Value;
            await DeferOtherPendingAnalysisJobsAsync(context, jobId);
        }

        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        var disabledWorker = new ProjectProposalAnalysisWorker(
            scopeFactory,
            new FixedAiFeatureGate(false),
            NullLogger<ProjectProposalAnalysisWorker>.Instance);
        (await disabledWorker.RunCycleAsync()).Should().Be(0);

        await using (var verificationScope = _factory.Services.CreateAsyncScope())
        {
            var context = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await context.ProjectProposalAnalysisJobs.AsNoTracking().SingleAsync(job => job.Id == jobId))
                .Status.Should().Be(ProjectProposalAnalysisJobStatus.Pending);
            (await context.ProjectProposalAnalysisResults.AsNoTracking().CountAsync(result => result.AnalysisJobId == jobId))
                .Should().Be(0);
            var proposalView = await CreateHandler(verificationScope, context, aiEnabled: false)
                .GetByTeamAsync(teamId, leaderUserId, SystemRoles.Student);
            proposalView.Value.CurrentAnalysisJobId.Should().BeNull();
            proposalView.Value.CurrentAnalysisStatus.Should().BeNull();
        }

        var enabledWorker = new ProjectProposalAnalysisWorker(
            scopeFactory,
            new FixedAiFeatureGate(true),
            NullLogger<ProjectProposalAnalysisWorker>.Instance);
        (await enabledWorker.RunCycleAsync()).Should().Be(1);
        (await enabledWorker.RunCycleAsync()).Should().Be(0);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var completedJob = await db.ProjectProposalAnalysisJobs.AsNoTracking().SingleAsync(job => job.Id == jobId);
        completedJob.Status.Should().Be(ProjectProposalAnalysisJobStatus.Completed);
        completedJob.AttemptCount.Should().Be(1);
        completedJob.LeaseOwner.Should().BeNull();
        completedJob.LeaseExpiresAtUtc.Should().BeNull();
        var result = await db.ProjectProposalAnalysisResults.AsNoTracking().SingleAsync(item => item.AnalysisJobId == jobId);
        result.Provider.Should().Be("Mock");
        result.OverlapRisk.Should().Be(ProjectProposalOverlapRisk.InsufficientData);

        var queryHandler = new ProjectProposalAnalysisQueryHandler(db, new FixedAiFeatureGate(true));
        var studentView = await queryHandler.GetAsync(jobId, leaderUserId, SystemRoles.Student);
        studentView.IsSuccess.Should().BeTrue(studentView.Error.Message);
        studentView.Value.Status.Should().Be(nameof(ProjectProposalAnalysisJobStatus.Completed));
        studentView.Value.CanViewDetailedReport.Should().BeFalse();
        studentView.Value.Report.Should().NotBeNull();
        studentView.Value.Report!.Limitations.Should().Contain(item => item.Contains("mô phỏng", StringComparison.OrdinalIgnoreCase));

        var hiddenWhenDisabled = await new ProjectProposalAnalysisQueryHandler(db, new FixedAiFeatureGate(false))
            .GetAsync(jobId, leaderUserId, SystemRoles.Student);
        hiddenWhenDisabled.IsFailure.Should().BeTrue();
        hiddenWhenDisabled.Error.Code.Should().Be(ErrorCodes.AiFeatureDisabled);
    }

    [Fact]
    public async Task AnalysisWorker_RecoversExpiredLease_WithoutDuplicateResult()
    {
        Guid jobId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seed = await CreateSeedAsync(context);
            context.ChangeTracker.Clear();
            var handler = CreateHandler(setupScope, context, aiEnabled: true);
            var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Expired lease"), seed.LeaderUserId, SystemRoles.Student);
            var submitted = await handler.SubmitAsync(created.Value.Id,
                new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion },
                seed.LeaderUserId,
                SystemRoles.Student);
            jobId = submitted.Value.CurrentAnalysisJobId!.Value;
            context.ChangeTracker.Clear();
            var job = await context.ProjectProposalAnalysisJobs.SingleAsync(candidate => candidate.Id == jobId);
            job.Status = ProjectProposalAnalysisJobStatus.Processing;
            job.AttemptCount = 1;
            job.ProcessingStartedAtUtc = DateTime.UtcNow.AddMinutes(-10);
            job.LeaseOwner = "expired-worker";
            job.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
            await context.SaveChangesAsync();
            await DeferOtherPendingAnalysisJobsAsync(context, jobId);
        }

        var worker = new ProjectProposalAnalysisWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAiFeatureGate(true),
            NullLogger<ProjectProposalAnalysisWorker>.Instance);
        (await worker.RunCycleAsync()).Should().Be(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobAfterRecovery = await db.ProjectProposalAnalysisJobs.AsNoTracking().SingleAsync(job => job.Id == jobId);
        jobAfterRecovery.Status.Should().Be(ProjectProposalAnalysisJobStatus.Completed);
        jobAfterRecovery.AttemptCount.Should().Be(2);
        (await db.ProjectProposalAnalysisResults.AsNoTracking().CountAsync(result => result.AnalysisJobId == jobId))
            .Should().Be(1);
    }

    [Fact]
    public async Task SemanticRetrieval_RanksClosestProposal_CachesEmbeddings_AndHidesEvidenceFromStudents()
    {
        Guid currentJobId;
        Guid currentVersionId;
        Guid similarVersionId;
        ProposalSeed currentSeed;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var similarSeed = await CreateSeedAsync(context);
            var unrelatedSeed = await CreateSeedAsync(context);
            currentSeed = await CreateSeedAsync(context);
            context.ChangeTracker.Clear();
            var handler = CreateHandler(setupScope, context, aiEnabled: true);

            var similar = await handler.CreateAsync(
                similarSeed.TeamId,
                ThemedDraft("uniquesolarwater", "Similar"),
                similarSeed.LeaderUserId,
                SystemRoles.Student);
            var similarSubmission = await handler.SubmitAsync(similar.Value.Id,
                new SubmitProjectProposalRequest { RowVersion = similar.Value.RowVersion },
                similarSeed.LeaderUserId,
                SystemRoles.Student);
            similarVersionId = similarSubmission.Value.CurrentSubmittedVersionId!.Value;

            var unrelated = await handler.CreateAsync(
                unrelatedSeed.TeamId,
                ThemedDraft("differentmedicalclinic", "Unrelated"),
                unrelatedSeed.LeaderUserId,
                SystemRoles.Student);
            await handler.SubmitAsync(unrelated.Value.Id,
                new SubmitProjectProposalRequest { RowVersion = unrelated.Value.RowVersion },
                unrelatedSeed.LeaderUserId,
                SystemRoles.Student);

            var current = await handler.CreateAsync(
                currentSeed.TeamId,
                ThemedDraft("uniquesolarwater", "Current"),
                currentSeed.LeaderUserId,
                SystemRoles.Student);
            var currentSubmission = await handler.SubmitAsync(current.Value.Id,
                new SubmitProjectProposalRequest { RowVersion = current.Value.RowVersion },
                currentSeed.LeaderUserId,
                SystemRoles.Student);
            currentJobId = currentSubmission.Value.CurrentAnalysisJobId!.Value;
            currentVersionId = currentSubmission.Value.CurrentSubmittedVersionId!.Value;
            await DeferOtherPendingAnalysisJobsAsync(context, currentJobId);
        }

        var worker = new ProjectProposalAnalysisWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAiFeatureGate(true),
            NullLogger<ProjectProposalAnalysisWorker>.Instance);
        (await worker.RunCycleAsync()).Should().Be(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var matches = await db.ProjectProposalAnalysisMatches.AsNoTracking()
            .Where(match => match.AnalysisResult.AnalysisJobId == currentJobId)
            .OrderBy(match => match.Rank)
            .ToArrayAsync();
        matches.Should().NotBeEmpty();
        matches.Should().HaveCountLessThanOrEqualTo(10);
        matches[0].CandidateProposalVersionId.Should().Be(similarVersionId);
        matches.Select(match => match.Rank).Should().Equal(Enumerable.Range(1, matches.Length));

        var relevantVersionIds = matches.Select(match => match.CandidateProposalVersionId)
            .Append(currentVersionId)
            .ToArray();
        var generatedBefore = await db.ProjectProposalEmbeddings.AsNoTracking()
            .Where(embedding => relevantVersionIds.Contains(embedding.ProposalVersionId))
            .ToDictionaryAsync(embedding => embedding.ProposalVersionId, embedding => embedding.GeneratedAtUtc);
        generatedBefore.Should().ContainKey(currentVersionId);
        generatedBefore.Should().ContainKey(similarVersionId);

        var studentView = await new ProjectProposalAnalysisQueryHandler(db, new FixedAiFeatureGate(true))
            .GetAsync(currentJobId, currentSeed.LeaderUserId, SystemRoles.Student);
        studentView.Value.CanViewDetailedReport.Should().BeFalse();
        studentView.Value.Report!.Matches.Should().BeEmpty();

        var lecturerView = await new ProjectProposalAnalysisQueryHandler(db, new FixedAiFeatureGate(true))
            .GetAsync(currentJobId, currentSeed.LecturerUserId, SystemRoles.Lecturer);
        lecturerView.IsSuccess.Should().BeTrue(lecturerView.Error.Message);
        lecturerView.Value.CanViewDetailedReport.Should().BeTrue();
        lecturerView.Value.Report!.Matches.First().ProposalVersionId.Should().Be(similarVersionId);

        db.ChangeTracker.Clear();
        var retriever = scope.ServiceProvider.GetRequiredService<IProposalSimilarityRetriever>();
        await retriever.RetrieveAsync(currentVersionId, includeCrossSemester: true);
        db.ChangeTracker.Clear();
        var generatedAfter = await db.ProjectProposalEmbeddings.AsNoTracking()
            .Where(embedding => relevantVersionIds.Contains(embedding.ProposalVersionId))
            .ToDictionaryAsync(embedding => embedding.ProposalVersionId, embedding => embedding.GeneratedAtUtc);
        generatedAfter.Should().BeEquivalentTo(generatedBefore);

        var staleEmbedding = await db.ProjectProposalEmbeddings.SingleAsync(embedding => embedding.ProposalVersionId == currentVersionId);
        staleEmbedding.Model = "obsolete-model";
        staleEmbedding.GeneratedAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await retriever.RetrieveAsync(currentVersionId, includeCrossSemester: true);
        db.ChangeTracker.Clear();
        var refreshedEmbedding = await db.ProjectProposalEmbeddings.AsNoTracking()
            .SingleAsync(embedding => embedding.ProposalVersionId == currentVersionId);
        refreshedEmbedding.Model.Should().Be("feature-hashing-384-v1");
        refreshedEmbedding.GeneratedAtUtc.Should().BeAfter(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task AnalysisFailure_DoesNotChangeSubmittedProposal()
    {
        Guid jobId;
        Guid proposalId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seed = await CreateSeedAsync(context);
            context.ChangeTracker.Clear();
            var handler = CreateHandler(setupScope, context, aiEnabled: true);
            var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Invalid snapshot"), seed.LeaderUserId, SystemRoles.Student);
            proposalId = created.Value.Id;
            var submitted = await handler.SubmitAsync(created.Value.Id,
                new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion },
                seed.LeaderUserId,
                SystemRoles.Student);
            jobId = submitted.Value.CurrentAnalysisJobId!.Value;
            context.ChangeTracker.Clear();
            var version = await context.ProjectProposalVersions.SingleAsync(item => item.Id == submitted.Value.CurrentSubmittedVersionId);
            version.SnapshotSchemaVersion = "unsupported-test-schema";
            await context.SaveChangesAsync();
            await DeferOtherPendingAnalysisJobsAsync(context, jobId);
        }

        var worker = new ProjectProposalAnalysisWorker(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAiFeatureGate(true),
            NullLogger<ProjectProposalAnalysisWorker>.Instance);
        (await worker.RunCycleAsync()).Should().Be(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var failedJob = await db.ProjectProposalAnalysisJobs.AsNoTracking().SingleAsync(job => job.Id == jobId);
        failedJob.Status.Should().Be(ProjectProposalAnalysisJobStatus.Failed);
        failedJob.LastErrorCode.Should().Be("PROPOSAL_ANALYSIS_SNAPSHOT_UNSUPPORTED");
        (await db.ProjectProposalAnalysisResults.AsNoTracking().AnyAsync(result => result.AnalysisJobId == jobId)).Should().BeFalse();
        (await db.ProjectProposals.AsNoTracking().SingleAsync(proposal => proposal.Id == proposalId))
            .Status.Should().Be(ProjectProposalStatus.Submitted);
    }

    [Fact]
    public async Task ActiveMemberCanCreateAndEditDraft_ButOnlyLeaderCanSubmit()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context);
        context.ChangeTracker.Clear();
        var handler = CreateHandler(scope, context);

        var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Initial"), seed.MemberUserId, SystemRoles.Student);

        created.IsSuccess.Should().BeTrue(created.Error.Message);
        created.Value.Status.Should().Be(nameof(ProjectProposalStatus.Draft));
        context.ChangeTracker.Clear();
        var persistedRowVersion = await context.ProjectProposals.AsNoTracking()
            .Where(proposal => proposal.Id == created.Value.Id)
            .Select(proposal => proposal.Version)
            .SingleAsync();
        created.Value.RowVersion.Should().Be(persistedRowVersion.ToString());
        (await context.ProjectProposalVersions.AsNoTracking().CountAsync(version => version.ProjectProposalId == created.Value.Id)).Should().Be(1);

        var updatedRequest = ValidUpdate(created.Value.RowVersion, "Member update");
        var updated = await handler.UpdateAsync(created.Value.Id, updatedRequest, seed.MemberUserId, SystemRoles.Student);
        updated.IsSuccess.Should().BeTrue(updated.Error.Message);
        context.ChangeTracker.Clear();

        var denied = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = updated.Value.RowVersion },
            seed.MemberUserId,
            SystemRoles.Student);
        denied.IsFailure.Should().BeTrue();
        denied.Error.Code.Should().Be(ErrorCodes.ProjectProposalAccessDenied);

        var submitted = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = updated.Value.RowVersion, ChangeNote = "Ready for review" },
            seed.LeaderUserId,
            SystemRoles.Student);
        submitted.IsSuccess.Should().BeTrue(submitted.Error.Message);
        submitted.Value.Status.Should().Be(nameof(ProjectProposalStatus.Submitted));
        submitted.Value.CurrentSubmittedVersionId.Should().NotBeNull();
        submitted.Value.CurrentAnalysisJobId.Should().BeNull();
        submitted.Value.CurrentAnalysisStatus.Should().BeNull();
        context.ChangeTracker.Clear();
        var versions = await context.ProjectProposalVersions.AsNoTracking()
            .Where(version => version.ProjectProposalId == created.Value.Id)
            .OrderBy(version => version.VersionNumber)
            .ToArrayAsync();
        versions.Should().HaveCount(3);
        versions.Last().Purpose.Should().Be(ProjectProposalVersionPurpose.Submission);
        (await context.ProjectProposalAnalysisJobs.AsNoTracking().CountAsync(job =>
            job.ProposalVersionId == submitted.Value.CurrentSubmittedVersionId)).Should().Be(0);
        (await context.OutboxMessages.AsNoTracking().AnyAsync(message =>
            message.Type == "ProjectProposal.Submitted.v1" && message.AggregateId == seed.ClassId)).Should().BeTrue();
    }

    [Fact]
    public async Task SubmitRequiresApprovedProjectDirection()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, ProjectDirectionStatus.Draft);
        context.ChangeTracker.Clear();
        var handler = CreateHandler(scope, context);
        var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Direction gate"), seed.LeaderUserId, SystemRoles.Student);

        var blocked = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion },
            seed.LeaderUserId,
            SystemRoles.Student);

        blocked.IsFailure.Should().BeTrue();
        blocked.Error.Code.Should().Be(ErrorCodes.ProjectProposalDirectionNotApproved);
        var direction = await context.ProjectDirections.SingleAsync(item => item.TeamId == seed.TeamId);
        direction.Status = ProjectDirectionStatus.Approved;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var submitted = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion },
            seed.LeaderUserId,
            SystemRoles.Student);
        submitted.IsSuccess.Should().BeTrue(submitted.Error.Message);
    }

    [Fact]
    public async Task IncompleteDraftCanBeSaved_ButCannotBeSubmitted()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context);
        context.ChangeTracker.Clear();
        var handler = CreateHandler(scope, context);
        var created = await handler.CreateAsync(seed.TeamId, new CreateProjectProposalRequest
        {
            Title = "Early draft",
            ChangeNote = "Save before all sections are ready"
        }, seed.LeaderUserId, SystemRoles.Student);

        created.IsSuccess.Should().BeTrue(created.Error.Message);
        var submitted = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion },
            seed.LeaderUserId,
            SystemRoles.Student);

        submitted.IsFailure.Should().BeTrue();
        submitted.Error.Code.Should().Be(ErrorCodes.ProjectProposalValidationError);
        context.ChangeTracker.Clear();
        (await context.ProjectProposalVersions.AsNoTracking().CountAsync(version => version.ProjectProposalId == created.Value.Id))
            .Should().Be(1);
        (await context.OutboxMessages.AsNoTracking().AnyAsync(message =>
            message.Type == "ProjectProposal.Submitted.v1" && message.AggregateId == seed.ClassId)).Should().BeFalse();
    }

    [Fact]
    public async Task NeedsRevisionCanBeEditedAndResubmitted_WithReviewBoundToEachSubmittedVersion()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context);
        context.ChangeTracker.Clear();
        var handler = CreateHandler(scope, context, aiEnabled: true);
        var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Review cycle"), seed.LeaderUserId, SystemRoles.Student);
        var firstSubmission = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion }, seed.LeaderUserId, SystemRoles.Student);
        firstSubmission.IsSuccess.Should().BeTrue(firstSubmission.Error.Message);
        context.ChangeTracker.Clear();

        var requestedRevision = await handler.ReviewAsync(created.Value.Id, new ReviewProjectProposalRequest
        {
            Decision = "NeedsRevision",
            Feedback = "Clarify the validated customer problem and proposed experiment.",
            RowVersion = firstSubmission.Value.RowVersion
        }, seed.LecturerUserId, SystemRoles.Lecturer);
        requestedRevision.IsSuccess.Should().BeTrue(requestedRevision.Error.Message);
        context.ChangeTracker.Clear();

        var revisedRequest = ValidUpdate(requestedRevision.Value.RowVersion, "Revision requested by lecturer");
        revisedRequest = CopyWithSolution(revisedRequest, revisedRequest.Solution + " The revision adds a measurable validation experiment.");
        var revised = await handler.UpdateAsync(created.Value.Id, revisedRequest, seed.MemberUserId, SystemRoles.Student);
        revised.IsSuccess.Should().BeTrue(revised.Error.Message);
        revised.Value.Status.Should().Be(nameof(ProjectProposalStatus.Draft));
        context.ChangeTracker.Clear();

        var secondSubmission = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = revised.Value.RowVersion, ChangeNote = "Resubmitted after review" },
            seed.LeaderUserId,
            SystemRoles.Student);
        secondSubmission.IsSuccess.Should().BeTrue(secondSubmission.Error.Message);
        secondSubmission.Value.CurrentAnalysisJobId.Should().NotBeNull();
        secondSubmission.Value.CurrentAnalysisStatus.Should().Be(nameof(ProjectProposalAnalysisJobStatus.Pending));
        context.ChangeTracker.Clear();
        var approved = await handler.ReviewAsync(created.Value.Id, new ReviewProjectProposalRequest
        {
            Decision = "Approved",
            Feedback = "The revision addresses the requested validation evidence.",
            RowVersion = secondSubmission.Value.RowVersion
        }, seed.LecturerUserId, SystemRoles.Lecturer);

        approved.IsSuccess.Should().BeTrue(approved.Error.Message);
        approved.Value.Status.Should().Be(nameof(ProjectProposalStatus.Approved));
        approved.Value.Reviews.Should().HaveCount(2);
        approved.Value.Reviews.Select(review => review.ProposalVersionId).Distinct().Should().HaveCount(2);
        context.ChangeTracker.Clear();
        (await context.ProjectProposalVersions.AsNoTracking().CountAsync(version =>
            version.ProjectProposalId == created.Value.Id && version.Purpose == ProjectProposalVersionPurpose.Submission)).Should().Be(2);
        var analysisJobs = await context.ProjectProposalAnalysisJobs.AsNoTracking()
            .Where(job => job.ProposalVersion.ProjectProposalId == created.Value.Id)
            .OrderBy(job => job.CreatedAtUtc)
            .ToArrayAsync();
        analysisJobs.Should().HaveCount(2);
        analysisJobs.Select(job => job.ProposalVersionId).Should().OnlyHaveUniqueItems();
        analysisJobs.Should().OnlyContain(job =>
            job.Status == ProjectProposalAnalysisJobStatus.Pending
            && job.CandidateScope == ProjectProposalAnalysisCandidateScope.AllSystem
            && job.IncludeCrossSemester
            && job.LanguageMode == ProjectProposalAnalysisLanguageMode.VietnameseAndEnglish
            && job.AttemptCount == 0);
    }

    [Fact]
    public async Task OutsiderAndUnassignedLecturerCannotAccessOrReviewProposal()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context);
        var outsider = await CreateUserAsync(context, SystemRoles.Student, "outsider");
        var otherLecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "other-lecturer");
        context.ChangeTracker.Clear();
        var handler = CreateHandler(scope, context, aiEnabled: true);
        var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Authorization"), seed.LeaderUserId, SystemRoles.Student);
        var submitted = await handler.SubmitAsync(created.Value.Id,
            new SubmitProjectProposalRequest { RowVersion = created.Value.RowVersion }, seed.LeaderUserId, SystemRoles.Student);
        context.ChangeTracker.Clear();

        var outsiderView = await handler.GetByTeamAsync(seed.TeamId, outsider.Id, SystemRoles.Student);
        var unrelatedReview = await handler.ReviewAsync(created.Value.Id, new ReviewProjectProposalRequest
        {
            Decision = "Approved",
            Feedback = "Attempted review from outside the assigned class.",
            RowVersion = submitted.Value.RowVersion
        }, otherLecturer.Id, SystemRoles.Lecturer);

        outsiderView.IsFailure.Should().BeTrue();
        outsiderView.Error.Code.Should().Be(ErrorCodes.ProjectProposalAccessDenied);
        unrelatedReview.IsFailure.Should().BeTrue();
        unrelatedReview.Error.Code.Should().Be(ErrorCodes.ProjectProposalAccessDenied);

        var analysisView = await new ProjectProposalAnalysisQueryHandler(context, new FixedAiFeatureGate(true))
            .GetAsync(submitted.Value.CurrentAnalysisJobId!.Value, outsider.Id, SystemRoles.Student);
        analysisView.IsFailure.Should().BeTrue();
        analysisView.Error.Code.Should().Be(ErrorCodes.ProjectProposalAnalysisAccessDenied);
    }

    [Fact]
    public async Task StaleRowVersionCannotCreateAnotherDraftVersion()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context);
        context.ChangeTracker.Clear();
        var handler = CreateHandler(scope, context);
        var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Concurrency"), seed.LeaderUserId, SystemRoles.Student);
        var staleVersion = created.Value.RowVersion;
        var firstUpdate = await handler.UpdateAsync(created.Value.Id, ValidUpdate(staleVersion, "First writer"), seed.MemberUserId, SystemRoles.Student);
        firstUpdate.IsSuccess.Should().BeTrue(firstUpdate.Error.Message);
        context.ChangeTracker.Clear();

        var staleUpdate = await handler.UpdateAsync(created.Value.Id, ValidUpdate(staleVersion, "Stale writer"), seed.LeaderUserId, SystemRoles.Student);

        staleUpdate.IsFailure.Should().BeTrue();
        staleUpdate.Error.Code.Should().Be(ErrorCodes.ProjectProposalConcurrencyConflict);
        context.ChangeTracker.Clear();
        (await context.ProjectProposalVersions.AsNoTracking().CountAsync(version => version.ProjectProposalId == created.Value.Id)).Should().Be(2);
    }

    [Fact]
    public async Task RestoreCreatesANewDraftVersion_WithoutMutatingHistoricalSnapshot()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context);
        context.ChangeTracker.Clear();
        var handler = CreateHandler(scope, context);
        var created = await handler.CreateAsync(seed.TeamId, ValidDraft("Original"), seed.LeaderUserId, SystemRoles.Student);
        var originalVersions = await handler.GetVersionsAsync(created.Value.Id, seed.LeaderUserId, SystemRoles.Student);
        var originalVersionId = originalVersions.Value.Single().Id;
        var originalSnapshot = await handler.GetVersionAsync(created.Value.Id, originalVersionId, seed.LeaderUserId, SystemRoles.Student);
        var originalSnapshotJson = await context.ProjectProposalVersions.AsNoTracking()
            .Where(version => version.Id == originalVersionId)
            .Select(version => version.SnapshotJson)
            .SingleAsync();
        var updated = await handler.UpdateAsync(created.Value.Id, ValidUpdate(created.Value.RowVersion, "Changed"), seed.MemberUserId, SystemRoles.Student);
        context.ChangeTracker.Clear();

        var restored = await handler.RestoreVersionAsync(created.Value.Id, originalVersionId,
            new RestoreProjectProposalVersionRequest { RowVersion = updated.Value.RowVersion, ChangeNote = "Restore original content" },
            seed.MemberUserId,
            SystemRoles.Student);

        restored.IsSuccess.Should().BeTrue(restored.Error.Message);
        restored.Value.Title.Should().Be(originalSnapshot.Value.Snapshot.Title);
        context.ChangeTracker.Clear();
        var versions = await context.ProjectProposalVersions.AsNoTracking()
            .Where(version => version.ProjectProposalId == created.Value.Id)
            .OrderBy(version => version.VersionNumber)
            .ToArrayAsync();
        versions.Should().HaveCount(3);
        versions[0].SnapshotJson.Should().Be(originalSnapshotJson);
        versions[2].Purpose.Should().Be(ProjectProposalVersionPurpose.DraftSave);
        versions[2].SnapshotJson.Should().Be(originalSnapshotJson);
    }

    private static ProjectProposalHandler CreateHandler(AsyncServiceScope scope, AppDbContext context, bool? aiEnabled = null) => new(
        context,
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
        scope.ServiceProvider.GetRequiredService<IDateTimeProvider>(),
        aiEnabled.HasValue
            ? new FixedAiFeatureGate(aiEnabled.Value)
            : scope.ServiceProvider.GetRequiredService<IAiFeatureGate>());

    private static CreateProjectProposalRequest ValidDraft(string suffix) => new()
    {
        Title = $"Circular agriculture platform {suffix}",
        StartupName = $"AgriLink {suffix}",
        Tagline = "Connecting farms directly with sustainable restaurant demand.",
        Problem = Text("Small farms struggle to predict restaurant demand and lose revenue through fragmented intermediaries.", 2),
        Solution = Text("The platform connects verified farms with restaurants and forecasts weekly demand using structured purchase signals.", 2),
        TargetCustomers = Text("Independent farms, agricultural cooperatives, restaurants, and institutional kitchens are the initial customers.", 1),
        ValueProposition = Text("Restaurants receive reliable local supply while farmers gain predictable demand, fairer pricing, and lower waste.", 1),
        MarketSize = "The initial market includes urban restaurants and nearby agricultural cooperatives.",
        Competitors = "General marketplaces lack demand forecasting and verified farm-to-restaurant workflows.",
        BusinessModel = Text("Restaurants subscribe for procurement tools while completed transactions generate a transparent service fee.", 2),
        RevenueModel = "Monthly subscriptions and transaction service fees.",
        MarketingStrategy = "Partner with restaurant associations and agricultural cooperatives.",
        Technology = "ASP.NET Core, React, PostgreSQL, forecasting services, and secure cloud infrastructure.",
        FinancialPlan = "Start with a controlled university-supported pilot and measure acquisition and transaction costs.",
        Roadmap = Text("Validate demand, onboard pilot farms and restaurants, measure repeat transactions, then expand to nearby cities.", 2),
        TeamIntroduction = "The team covers product, engineering, market research, and operations.",
        ChangeNote = $"{suffix} draft"
    };

    private static UpdateProjectProposalRequest ValidUpdate(string rowVersion, string suffix)
    {
        var draft = ValidDraft(suffix);
        return new UpdateProjectProposalRequest
        {
            Title = draft.Title,
            StartupName = draft.StartupName,
            Tagline = draft.Tagline,
            Problem = draft.Problem,
            Solution = draft.Solution,
            TargetCustomers = draft.TargetCustomers,
            ValueProposition = draft.ValueProposition,
            MarketSize = draft.MarketSize,
            Competitors = draft.Competitors,
            BusinessModel = draft.BusinessModel,
            RevenueModel = draft.RevenueModel,
            MarketingStrategy = draft.MarketingStrategy,
            Technology = draft.Technology,
            FinancialPlan = draft.FinancialPlan,
            Roadmap = draft.Roadmap,
            TeamIntroduction = draft.TeamIntroduction,
            ChangeNote = draft.ChangeNote,
            RowVersion = rowVersion
        };
    }

    private static CreateProjectProposalRequest ThemedDraft(string token, string suffix)
    {
        var detail = Text($"{token} platform connects verified users with measurable operational outcomes and transparent workflows.", 3);
        return new CreateProjectProposalRequest
        {
            Title = $"{token} platform {suffix}",
            StartupName = $"{token} {suffix}",
            Tagline = $"A focused {token} service.",
            Problem = detail,
            Solution = detail,
            TargetCustomers = detail,
            ValueProposition = detail,
            MarketSize = detail,
            Competitors = detail,
            BusinessModel = detail,
            RevenueModel = detail,
            MarketingStrategy = detail,
            Technology = detail,
            FinancialPlan = detail,
            Roadmap = detail,
            TeamIntroduction = detail,
            ChangeNote = $"{suffix} semantic retrieval test"
        };
    }

    private static UpdateProjectProposalRequest CopyWithSolution(UpdateProjectProposalRequest request, string solution) => new()
    {
        Title = request.Title, StartupName = request.StartupName, Tagline = request.Tagline,
        Problem = request.Problem, Solution = solution, TargetCustomers = request.TargetCustomers,
        ValueProposition = request.ValueProposition, MarketSize = request.MarketSize, Competitors = request.Competitors,
        BusinessModel = request.BusinessModel, RevenueModel = request.RevenueModel, MarketingStrategy = request.MarketingStrategy,
        Technology = request.Technology, FinancialPlan = request.FinancialPlan, Roadmap = request.Roadmap,
        TeamIntroduction = request.TeamIntroduction, ChangeNote = request.ChangeNote, RowVersion = request.RowVersion
    };

    private static string Text(string sentence, int repetitions) => string.Join(' ', Enumerable.Repeat(sentence, repetitions));

    private static Task<int> DeferOtherPendingAnalysisJobsAsync(AppDbContext context, Guid jobId) =>
        context.ProjectProposalAnalysisJobs
            .Where(job => job.Id != jobId && job.Status == ProjectProposalAnalysisJobStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(job => job.AvailableAtUtc, DateTime.UtcNow.AddDays(1)));

    private static async Task<ProposalSeed> CreateSeedAsync(
        AppDbContext context,
        ProjectDirectionStatus directionStatus = ProjectDirectionStatus.Approved)
    {
        var admin = await context.Users.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin));
        var lecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "proposal-owner");
        var leaderUser = await CreateUserAsync(context, SystemRoles.Student, "proposal-leader");
        var memberUser = await CreateUserAsync(context, SystemRoles.Student, "proposal-member");
        var unique = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var semester = await context.Semesters.FirstAsync(item => item.Status == SemesterStatus.Active);
        var course = new Course { Code = $"PP{unique}", Name = $"Proposal Lifecycle {unique}", Status = CourseStatus.Active, CreatedBy = admin.Id };
        var targetClass = new Class
        {
            ClassCode = $"PP{unique}_1",
            Slug = ClassSlugRules.BuildBaseSlug(semester.Code, course.Code, 1),
            ClassIndex = 1,
            Course = course,
            CourseId = course.Id,
            Semester = semester,
            SemesterId = semester.Id,
            PrimaryLecturer = lecturer,
            PrimaryLecturerId = lecturer.Id,
            Status = ClassStatus.Active,
            ScheduleJson = "[{\"dayOfWeek\":3,\"slotNumber\":2,\"room\":\"PP-201\"}]",
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };
        context.Courses.Add(course);
        context.Classes.Add(targetClass);
        context.ClassLecturers.Add(new ClassLecturer
        {
            Class = targetClass,
            ClassId = targetClass.Id,
            Lecturer = lecturer,
            LecturerId = lecturer.Id,
            IsPrimary = true,
            AssignedById = admin.Id
        });

        var leader = CreateStudent(leaderUser, $"PL{unique}", admin.Id);
        var member = CreateStudent(memberUser, $"PM{unique}", admin.Id);
        context.Students.AddRange(leader, member);
        var leaderEnrollment = CreateEnrollment(targetClass, semester, course, leader);
        var memberEnrollment = CreateEnrollment(targetClass, semester, course, member);
        context.ClassStudents.AddRange(leaderEnrollment, memberEnrollment);

        var team = new Team
        {
            Class = targetClass,
            ClassId = targetClass.Id,
            TeamCode = $"{targetClass.ClassCode}_T1",
            TeamName = $"Proposal Team {unique}",
            Status = TeamStatus.Active,
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };
        team.TeamMembers.Add(new TeamMember
        {
            Team = team, TeamId = team.Id, ClassId = targetClass.Id, StudentId = leader.Id,
            ClassStudent = leaderEnrollment, RoleInTeam = TeamMemberRole.Leader, CountsTowardActiveTeam = true,
            JoinedAt = DateTime.UtcNow, CreatedById = admin.Id
        });
        team.TeamMembers.Add(new TeamMember
        {
            Team = team, TeamId = team.Id, ClassId = targetClass.Id, StudentId = member.Id,
            ClassStudent = memberEnrollment, RoleInTeam = TeamMemberRole.Member, CountsTowardActiveTeam = true,
            JoinedAt = DateTime.UtcNow, CreatedById = admin.Id
        });
        var project = new Project
        {
            Team = team,
            TeamId = team.Id,
            Name = $"AgriLink {unique}",
            Description = "A verified farm-to-restaurant procurement and demand planning platform.",
            Status = ProjectStatus.Draft,
            CreatedById = leaderUser.Id,
            CreatedBy = leaderUser.Id
        };
        project.ProjectTags.Add(new ProjectTag
        {
            Project = project,
            ProjectId = project.Id,
            TagName = "Agriculture",
            NormalizedTagName = "AGRICULTURE",
            TagType = ProjectTagType.StartupField,
            CreatedById = leaderUser.Id,
            CreatedAt = DateTime.UtcNow
        });
        team.Project = project;
        team.ProjectDirection = new ProjectDirection
        {
            Team = team,
            TeamId = team.Id,
            Title = project.Name,
            Summary = project.Description,
            Status = directionStatus,
            SubmittedAtUtc = DateTime.UtcNow,
            ReviewedAtUtc = directionStatus == ProjectDirectionStatus.Approved ? DateTime.UtcNow : null,
            ReviewedByUserId = directionStatus == ProjectDirectionStatus.Approved ? lecturer.Id : null,
            CreatedBy = leaderUser.Id
        };
        context.Teams.Add(team);
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return new ProposalSeed(targetClass.Id, team.Id, leaderUser.Id, memberUser.Id, lecturer.Id);
    }

    private static Student CreateStudent(User user, string rollNumber, Guid adminId) => new()
    {
        User = user,
        UserId = user.Id,
        RollNumber = rollNumber,
        NormalizedRollNumber = rollNumber,
        FullName = user.FullName,
        Email = user.Email,
        MajorCode = MajorCodes.BIT_SE,
        Status = StudentStatus.Active,
        CreatedBy = adminId
    };

    private static ClassStudent CreateEnrollment(Class targetClass, Semester semester, Course course, Student student) => new()
    {
        Class = targetClass,
        ClassId = targetClass.Id,
        Student = student,
        StudentId = student.Id,
        SemesterId = semester.Id,
        CourseId = course.Id,
        EnrollmentStatus = EnrollmentStatus.Active,
        CountsTowardCourseSemesterLimit = true,
        MajorCodeAtEnrollment = MajorCodes.BIT_SE
    };

    private static async Task<User> CreateUserAsync(AppDbContext context, string roleName, string suffix)
    {
        var role = await context.Roles.SingleAsync(item => item.Name == roleName);
        var email = $"proposal-{suffix}-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            FullName = $"Proposal {suffix}",
            Email = email,
            NormalizedEmail = email.ToLowerInvariant(),
            PasswordHash = "integration-test-only",
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        user.UserRoles.Add(new UserRole { User = user, UserId = user.Id, Role = role, RoleId = role.Id });
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed record ProposalSeed(Guid ClassId, Guid TeamId, Guid LeaderUserId, Guid MemberUserId, Guid LecturerUserId);

    private sealed class FixedAiFeatureGate(bool isEnabled) : IAiFeatureGate
    {
        public bool IsEnabled { get; } = isEnabled;
    }
}
