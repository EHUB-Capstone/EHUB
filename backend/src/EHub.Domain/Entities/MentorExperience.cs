namespace EHub.Domain.Entities;

public sealed class MentorExperience
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MentorProfileId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public decimal? Years { get; set; }
    public string? Level { get; set; }
    public string? Notes { get; set; }
}
