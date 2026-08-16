using EHub.Application.Features.Classes.Common;
using EHub.Shared.Constants;
using FluentAssertions;

namespace EHub.UnitTests.Common;

public sealed class StudentEnrollmentRulesTests
{
    [Fact]
    public void ValidateAndNormalize_WithValidContract_NormalizesAllIdentityFields()
    {
        var error = StudentEnrollmentRules.ValidateAndNormalize(
            " se123456 ",
            " Nguyen Van A ",
            " A@FPT.EDU.VN ",
            " bit_se ",
            out var input);

        error.Should().BeNull();
        input.StudentCode.Should().Be("SE123456");
        input.FullName.Should().Be("Nguyen Van A");
        input.Email.Should().Be("a@fpt.edu.vn");
        input.MajorCode.Should().Be("BIT_SE");
    }

    [Fact]
    public void ValidateAndNormalize_WithUnknownMajor_ReturnsValidationError()
    {
        var error = StudentEnrollmentRules.ValidateAndNormalize(
            "SE123456",
            "Nguyen Van A",
            "a@fpt.edu.vn",
            "NOT_A_MAJOR",
            out _);

        error.Should().Contain("invalid");
    }

    [Fact]
    public void ValidateAndNormalize_WithMissingMajor_AllowsExplicitDeferredResolution()
    {
        var error = StudentEnrollmentRules.ValidateAndNormalize(
            "SE123456",
            "Nguyen Van A",
            "a@fpt.edu.vn",
            null,
            out var input,
            allowMissingMajor: true);

        error.Should().BeNull();
        input.MajorCode.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAndNormalize_WithMissingMajor_RejectsItByDefault()
    {
        var error = StudentEnrollmentRules.ValidateAndNormalize(
            "SE123456",
            "Nguyen Van A",
            "a@fpt.edu.vn",
            null,
            out _);

        error.Should().Be("Major code is required.");
    }

    [Fact]
    public void ValidateAndNormalize_WithUndeclaredMajor_RequiresExplicitImportOptIn()
    {
        var defaultError = StudentEnrollmentRules.ValidateAndNormalize(
            "SE123456",
            "Nguyen Van A",
            "a@fpt.edu.vn",
            MajorCodes.Undeclared,
            out _);

        var importError = StudentEnrollmentRules.ValidateAndNormalize(
            "SE123456",
            "Nguyen Van A",
            "a@fpt.edu.vn",
            MajorCodes.Undeclared,
            out var input,
            allowUndeclaredMajor: true);

        defaultError.Should().Contain("invalid");
        importError.Should().BeNull();
        input.MajorCode.Should().Be(MajorCodes.Undeclared);
    }
}
