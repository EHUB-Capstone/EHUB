using EHub.Application.Features.ProjectProposals;
using EHub.Contracts.ProjectProposals;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProjectProposals;

public sealed class ProjectProposalValidatorsTests
{
    [Fact]
    public async Task CreateDraft_AllowsIncompleteContentWithinLimits()
    {
        var result = await new CreateProjectProposalRequestValidator().ValidateAsync(new CreateProjectProposalRequest
        {
            Title = string.Empty,
            StartupName = string.Empty,
            ChangeNote = "Initial incomplete draft"
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDraft_RejectsFieldAndTotalLengthLimits()
    {
        var fieldResult = await new CreateProjectProposalRequestValidator().ValidateAsync(new CreateProjectProposalRequest
        {
            Problem = new string('a', 3_001)
        });
        var totalResult = await new CreateProjectProposalRequestValidator().ValidateAsync(new CreateProjectProposalRequest
        {
            Problem = new string('a', 3_000),
            Solution = new string('a', 3_000),
            TargetCustomers = new string('a', 2_000),
            ValueProposition = new string('a', 2_000),
            MarketSize = new string('a', 2_500),
            Competitors = new string('a', 3_000),
            BusinessModel = new string('a', 3_000),
            RevenueModel = new string('a', 2_000),
            MarketingStrategy = new string('a', 3_000),
            Technology = new string('a', 3_000),
            FinancialPlan = new string('a', 3_000),
            Roadmap = new string('a', 3_000)
        });

        fieldResult.IsValid.Should().BeFalse();
        fieldResult.Errors.Should().Contain(error => error.PropertyName == nameof(CreateProjectProposalRequest.Problem));
        totalResult.IsValid.Should().BeFalse();
        totalResult.Errors.Should().Contain(error => error.ErrorMessage.Contains("30000"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task StateChangingRequests_RequireNumericRowVersion(string rowVersion)
    {
        (await new SubmitProjectProposalRequestValidator().ValidateAsync(new SubmitProjectProposalRequest { RowVersion = rowVersion }))
            .IsValid.Should().BeFalse();
        (await new RestoreProjectProposalVersionRequestValidator().ValidateAsync(new RestoreProjectProposalVersionRequest { RowVersion = rowVersion }))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("NeedsRevision")]
    [InlineData("Rejected")]
    public async Task Review_AllowsOnlySupportedDecisions(string decision)
    {
        var result = await new ReviewProjectProposalRequestValidator().ValidateAsync(new ReviewProjectProposalRequest
        {
            Decision = decision,
            Feedback = "A valid lecturer review.",
            RowVersion = "1"
        });

        result.IsValid.Should().BeTrue();
    }
}
