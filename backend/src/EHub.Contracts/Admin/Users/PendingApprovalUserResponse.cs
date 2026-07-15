using System;
using System.Collections.Generic;

namespace EHub.Contracts.Admin.Users;

public sealed class PendingApprovalUserResponse
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
