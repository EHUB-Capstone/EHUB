using System;

namespace EHub.Contracts.Auth;

public sealed class UpdateProfileResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
