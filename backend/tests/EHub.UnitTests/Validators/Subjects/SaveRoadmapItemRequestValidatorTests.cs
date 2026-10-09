using EHub.Application.Validators.Subjects;
using EHub.Contracts.Subjects;

namespace EHub.UnitTests.Validators.Subjects;

public sealed class SaveRoadmapItemRequestValidatorTests
{
    private readonly SaveRoadmapItemRequestValidator _validator = new();

    [Theory]
    [InlineData("", "Description")]
    [InlineData("Title", "")]
    [InlineData("   ", "Description")]
    [InlineData("Title", "   ")]
    public void Validate_WhenTitleOrDescriptionIsBlank_ReturnsValidationError(string title, string description)
    {
        var result = _validator.Validate(ValidRequest(title, description));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == (string.IsNullOrWhiteSpace(title)
                ? nameof(SaveRoadmapItemRequest.Title)
                : nameof(SaveRoadmapItemRequest.Description)));
    }

    [Fact]
    public void Validate_WhenRequiredContentIsPresent_Succeeds()
    {
        var result = _validator.Validate(ValidRequest("Problem discovery", "Interview target users."));

        Assert.True(result.IsValid);
    }

    private static SaveRoadmapItemRequest ValidRequest(string title, string description) => new()
    {
        Title = title,
        Description = description,
        CourseCode = "EXE101",
        WeekNumber = 1,
        Priority = "MEDIUM"
    };
}
