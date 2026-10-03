using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

public sealed partial class TeamWorkflowIntegrationTests
{
    [Fact]
    public async Task EvaluationReportExport_RequiresStaffAndChecksTeamScope()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        var student = await context.Users.SingleAsync(user => user.Id == seed.ProposerUserId);
        var lecturer = await context.Users.SingleAsync(user => user.Id == seed.LecturerId);
        var courseId = await context.Classes.Where(item => item.Id == seed.ClassId)
            .Select(item => item.CourseId).SingleAsync();
        var project = new Project
        {
            TeamId = seed.TeamId!.Value,
            Name = "Evaluation export project",
            Status = ProjectStatus.Draft,
            CreatedById = seed.ProposerUserId,
        };
        var assessment = new Rubric
        {
            CourseId = courseId,
            Name = "Business Model",
            Status = RubricStatus.Active,
            CourseWeight = 20,
            TotalWeight = 100,
            CreatedById = seed.AdminId,
        };
        var checkpoint = new Checkpoint
        {
            CourseId = courseId,
            Name = "Prototype Review",
            CheckpointNumber = 1,
            Status = CheckpointStatus.Open,
            CreatedById = seed.AdminId,
        };
        var checkpointRubric = new Rubric
        {
            CourseId = courseId,
            Checkpoint = checkpoint,
            Name = "Prototype rubric",
            Status = RubricStatus.Active,
            TotalWeight = 100,
            CreatedById = seed.AdminId,
            Criteria = new List<RubricCriterion>
            {
                new() { Key = "quality", Name = "Quality", Weight = 100, MaxScore = 10, DisplayOrder = 1 },
            },
        };
        context.AddRange(project, assessment, checkpoint, checkpointRubric);
        await context.SaveChangesAsync();
        var assessmentEvaluation = new Evaluation
        {
            ProjectId = project.Id,
            RubricId = assessment.Id,
            EvaluatorId = seed.LecturerId,
            EvaluatorRole = EvaluatorRole.Lecturer,
            TotalScore = 8.5m,
            MaxTotalScore = 10,
            Status = EvaluationStatus.Submitted,
            SubmittedAt = DateTime.UtcNow,
            CreatedBy = seed.LecturerId,
        };
        assessmentEvaluation.MemberScores.Add(new EvaluationMemberScore
        {
            StudentId = seed.StudentIds[0],
            Score = 6.5m,
            CreatedBy = seed.LecturerId,
        });
        var checkpointEvaluation = new Evaluation
        {
            ProjectId = project.Id,
            RubricId = checkpointRubric.Id,
            EvaluatorId = seed.LecturerId,
            EvaluatorRole = EvaluatorRole.Lecturer,
            TotalScore = 9m,
            MaxTotalScore = 10,
            Status = EvaluationStatus.Published,
            SubmittedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
            CreatedBy = seed.LecturerId,
        };
        checkpointEvaluation.MemberScores.Add(new EvaluationMemberScore
        {
            StudentId = seed.StudentIds[1],
            Score = 7.25m,
            CreatedBy = seed.LecturerId,
        });
        context.Evaluations.AddRange(assessmentEvaluation, checkpointEvaluation);
        await context.SaveChangesAsync();
        var rollNumbers = await context.Students.AsNoTracking()
            .Where(item => seed.StudentIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.RollNumber ?? string.Empty);
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        using var client = _factory.CreateClient();
        const string url = "/api/workspace/checkpoints/evaluation-grading/export";
        var validPayload = $"{{\"teams\":[{{\"teamId\":\"{seed.TeamId!.Value}\",\"checkpointNumbers\":[1]}}]}}";

        using var anonymous = await client.PostAsync(url,
            new StringContent(validPayload, Encoding.UTF8, "application/json"));
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var studentRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(validPayload, Encoding.UTF8, "application/json"),
        };
        studentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            tokenService.GenerateAccessToken(student, [SystemRoles.Student]).Token);
        using var forbiddenStudent = await client.SendAsync(studentRequest);
        forbiddenStudent.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var inaccessibleRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                $"{{\"teams\":[{{\"teamId\":\"{Guid.NewGuid()}\",\"checkpointNumbers\":[1]}}]}}",
                Encoding.UTF8,
                "application/json"),
        };
        inaccessibleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            tokenService.GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token);
        using var forbiddenTeam = await client.SendAsync(inaccessibleRequest);
        forbiddenTeam.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var lecturerRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(validPayload, Encoding.UTF8, "application/json"),
        };
        lecturerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            tokenService.GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token);
        using var success = await client.SendAsync(lecturerRequest);
        success.StatusCode.Should().Be(HttpStatusCode.OK);
        success.Content.Headers.ContentType?.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var reportBytes = await success.Content.ReadAsByteArrayAsync();
        reportBytes.Should().NotBeEmpty();
        using var workbook = new XLWorkbook(new MemoryStream(reportBytes));
        var worksheet = workbook.Worksheet("Evaluation Report");
        worksheet.Cell(1, 1).GetString().Should().Be("MSSV");
        worksheet.Cell(1, 2).GetString().Should().Be("Business Model");
        worksheet.Cell(1, 3).GetString().Should().Be("Checkpoint 1");
        var rowsByRollNumber = worksheet.RowsUsed().Skip(1)
            .ToDictionary(row => row.Cell(1).GetString());
        rowsByRollNumber[rollNumbers[seed.StudentIds[0]]].Cell(2).GetValue<decimal>().Should().Be(6.5m);
        rowsByRollNumber[rollNumbers[seed.StudentIds[0]]].Cell(3).GetValue<decimal>().Should().Be(9m);
        rowsByRollNumber[rollNumbers[seed.StudentIds[1]]].Cell(2).GetValue<decimal>().Should().Be(8.5m);
        rowsByRollNumber[rollNumbers[seed.StudentIds[1]]].Cell(3).GetValue<decimal>().Should().Be(7.25m);
    }
}
