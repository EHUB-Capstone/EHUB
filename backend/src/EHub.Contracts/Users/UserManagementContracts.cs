namespace EHub.Contracts.Users;

public sealed class SaveManagedUserRequest
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Password { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = "APPROVED";
    public string? Phone { get; init; }
    public string? Bio { get; init; }
    public string? StudentId { get; init; }
    public string? ProgramGroup { get; init; }
    public string? Major { get; init; }
}

public sealed class ManagedUserResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public string Role { get; init; } = "STUDENT";
    public string Status { get; init; } = "APPROVED";
    public string? StudentId { get; init; }
    public string? ProgramGroup { get; init; }
    public string? Major { get; init; }
    public string? Phone { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ManagedUserListResponse
{
    public IReadOnlyCollection<ManagedUserResponse> Users { get; init; } = Array.Empty<ManagedUserResponse>();
    public PaginationResponse Pagination { get; init; } = new();
}

public sealed class PaginationResponse { public int Total { get; init; } public int Page { get; init; } public int Limit { get; init; } public int Pages { get; init; } }
