using System;
using System.Collections.Generic;

namespace EHub.Contracts.Auth;

public sealed class CurrentUserResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public string Status { get; init; } = string.Empty;
    public string? MajorCode { get; init; }
}
