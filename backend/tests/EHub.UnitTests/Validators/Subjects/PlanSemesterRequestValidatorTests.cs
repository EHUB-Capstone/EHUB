using EHub.Application.Validators.Subjects;
using EHub.Contracts.Subjects;

namespace EHub.UnitTests.Validators.Subjects;

public sealed class PlanSemesterRequestValidatorTests
{
    private readonly PlanSemesterRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenFallEndsInJanuaryOfFollowingYear_Succeeds()
    {
        var request = Request("FA", new DateOnly(2027, 9, 1), new DateOnly(2028, 1, 15));

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("SP", 2028, 1)]
    [InlineData("SU", 2028, 1)]
    [InlineData("FA", 2028, 2)]
    public void Validate_WhenEndDateExceedsAllowedSemesterYear_ReturnsValidationError(
        string semester,
        int endYear,
        int endMonth)
    {
        var request = Request(semester, new DateOnly(2027, 9, 1), new DateOnly(endYear, endMonth, 1));

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WhenEndDateIsNotAfterStartDate_ReturnsValidationError()
    {
        var request = Request("SP", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 1));

        var result = _validator.Validate(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(PlanSemesterRequest.EndDate));
    }

    private static PlanSemesterRequest Request(string semester, DateOnly startDate, DateOnly endDate) => new()
    {
        Semester = semester,
        Year = 2027,
        StartDate = startDate,
        EndDate = endDate,
    };
}
