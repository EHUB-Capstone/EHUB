using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class MentorAllocationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminUserId { get; set; }
    public User AdminUser { get; set; } = null!;
    public Guid SemesterId { get; set; }
    public Semester Semester { get; set; } = null!;
    public string RequestHash { get; set; } = string.Empty;
    public string RequestJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public MentorAllocationJobStatus Status { get; set; }
    public int CompletedBatches { get; set; }
    public int TotalBatches { get; set; }
    public int Attempts { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public uint Version { get; set; }
}
