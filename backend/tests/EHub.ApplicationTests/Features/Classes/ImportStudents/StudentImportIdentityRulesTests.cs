using EHub.Application.Features.Classes.ImportStudents;
using EHub.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ImportStudents;

public sealed class StudentImportIdentityRulesTests
{
    [Fact]
    public void ExistingProfileByEmail_WhenStudentCodeIsMissing_CanBeCompleted()
    {
        var profile = new Student
        {
            Email = "kienltde180359@fpt.edu.vn",
            FullName = "Le Trung Kien",
            UserId = Guid.NewGuid()
        };

        var hasConflict = StudentImportIdentityRules.HasConflict(
            0,
            1,
            null,
            profile,
            "DE180359",
            profile.Email);
        var changed = StudentImportIdentityRules.CompleteMissingIdentity(
            profile,
            "DE180359",
            profile.Email);

        hasConflict.Should().BeFalse();
        changed.Should().BeTrue();
        profile.RollNumber.Should().Be("DE180359");
        profile.NormalizedRollNumber.Should().Be("DE180359");
    }

    [Fact]
    public void ExistingProfileByEmail_WhenStudentCodeDiffers_IsAConflict()
    {
        var profile = new Student
        {
            RollNumber = "DE180358",
            NormalizedRollNumber = "DE180358",
            Email = "kienltde180359@fpt.edu.vn",
            FullName = "Le Trung Kien"
        };

        var hasConflict = StudentImportIdentityRules.HasConflict(
            0,
            1,
            null,
            profile,
            "DE180359",
            profile.Email);

        hasConflict.Should().BeTrue();
    }

    [Fact]
    public void ExistingProfileByCode_WhenEmailIsMissing_CanBeCompleted()
    {
        var profile = new Student
        {
            RollNumber = "DE180359",
            NormalizedRollNumber = "DE180359",
            FullName = "Le Trung Kien"
        };

        var hasConflict = StudentImportIdentityRules.HasConflict(
            1,
            0,
            profile,
            null,
            profile.RollNumber,
            "kienltde180359@fpt.edu.vn");
        var changed = StudentImportIdentityRules.CompleteMissingIdentity(
            profile,
            profile.RollNumber,
            "kienltde180359@fpt.edu.vn");

        hasConflict.Should().BeFalse();
        changed.Should().BeTrue();
        profile.Email.Should().Be("kienltde180359@fpt.edu.vn");
    }
}
