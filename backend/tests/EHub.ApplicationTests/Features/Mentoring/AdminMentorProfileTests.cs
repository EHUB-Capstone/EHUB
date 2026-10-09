using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Mentoring;
using EHub.Application.Features.Mentoring.ManageProfiles;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class AdminMentorProfileTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static SaveAdminMentorProfileRequest Request(string email = "mentor@example.test", string area = "AI", decimal? years = 3,
        string[]? tags = null) => new()
    {
        FullName = "Demo Mentor", Email = email, TemporaryPassword = "Demo@123456",
        Profile = new UpdateMentorProfileRequest { MentorType = "Business", Expertise = ["Product"],
            Bio = "Startup advisor", StartupDomains = ["Education"], TechnologySkills = ["AI"], Tags = tags ?? ["Founder"],
            Experiences = [new MentorExperienceDto { Kind = "Technology", Area = area, Years = years, Level = "Advanced", Notes = "Builds products" }] }
    };
    private static ICurrentUserService User(string role)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid()); user.Roles.Returns(new[] { role });
        return user;
    }

    [Fact]
    public async Task AdminCreatesAndUpdatesAccountAndStructuredProfileAtomically()
    {
        await using var context = Context();
        context.Roles.Add(new Role { Name = SystemRoles.Mentor }); await context.SaveChangesAsync();
        var hasher = Substitute.For<IPasswordHasher>(); hasher.Hash(Arg.Any<string>()).Returns("hashed-test-password");
        var handler = new AdminMentorProfileHandler(context, User(SystemRoles.Admin), hasher);
        var created = await handler.SaveAsync(null, Request(), default);
        created.IsSuccess.Should().BeTrue();
        var profile = await context.MentorProfiles.Include(x => x.User).Include(x => x.Experiences).SingleAsync();
        profile.User.PasswordHash.Should().Be("hashed-test-password");
        profile.Experiences.Single().Years.Should().Be(3);
        (await context.UserRoles.CountAsync()).Should().Be(1);
        var updated = await handler.SaveAsync(profile.Id, Request("updated@example.test", "Cloud", 5), default);
        updated.IsSuccess.Should().BeTrue(updated.IsFailure ? updated.Error.Message : string.Empty);
        context.ChangeTracker.Clear();
        var stored = await context.MentorProfiles.Include(x => x.User).Include(x => x.Experiences).SingleAsync();
        stored.User.NormalizedEmail.Should().Be("updated@example.test");
        stored.Experiences.Should().ContainSingle().Which.Area.Should().Be("Cloud");
        var reader = new MentorProfileHandler(context, Substitute.For<IMentorDocumentStorageService>(), Substitute.For<IMentorEmbeddingSearch>());
        var mine = await reader.GetMineAsync(stored.UserId, default);
        mine.Value.Experiences.Single().Years.Should().Be(5);
        mine.Value.TechnologySkills.Should().Contain("AI");
        (await context.Users.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("Lecturer")]
    [InlineData("Mentor")]
    [InlineData("Student")]
    public async Task NonAdminCannotManageProfiles(string role)
    {
        await using var context = Context();
        var handler = new AdminMentorProfileHandler(context, User(role), Substitute.For<IPasswordHasher>());
        var result = await handler.SaveAsync(null, Request(), default);
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        (await context.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DuplicateEmailAndInvalidMetadataDoNotCreateAccounts()
    {
        await using var context = Context();
        context.Users.Add(new User { FullName = "Existing", Email = "mentor@example.test", NormalizedEmail = "mentor@example.test" });
        await context.SaveChangesAsync();
        var handler = new AdminMentorProfileHandler(context, User(SystemRoles.Admin), Substitute.For<IPasswordHasher>());
        var duplicate = await handler.SaveAsync(null, Request("MENTOR@example.test"), default);
        duplicate.Error.Code.Should().Be(ErrorCodes.CommonValidationError);
        var tags = await handler.SaveAsync(null, Request("new@example.test", tags: ["AI", " ai "]), default);
        tags.Error.Code.Should().Be(ErrorCodes.CommonValidationError);
        var years = await handler.SaveAsync(null, Request("new@example.test", years: -1), default);
        years.Error.Code.Should().Be(ErrorCodes.CommonValidationError);
        (await context.MentorProfiles.CountAsync()).Should().Be(0);
        (await context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void ExperienceAndTagsContributeToRecommendation()
    {
        var profile = new MentorProfile { Expertise = ["Product"], StartupDomains = ["Education"],
            TechnologySkills = ["Cloud"], Tags = ["Founder"], Experiences = [new MentorExperience { Kind = "Technology", Area = "AI", Years = 5 }] };
        var result = MentorFitScorer.Score(profile, "AI cloud education platform", 0, 0);
        result.Reasons.Should().Contain("Chuyên môn phù hợp: AI").And.Contain("Chuyên môn phù hợp: Cloud");
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DuplicatedExperienceIsRejected()
    {
        var request = new UpdateMentorProfileRequest { MentorType = "IT", Expertise = ["AI"],
            Experiences = [new MentorExperienceDto { Kind = "Technology", Area = "AI" }, new MentorExperienceDto { Kind = "Technology", Area = " ai " }] };
        (await new UpdateMentorProfileRequestValidator().ValidateAsync(request)).IsValid.Should().BeFalse();
    }
}
