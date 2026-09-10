namespace EHub.Application.Features.Auth.UpdateProfile;

public sealed class UpdateProfileCommand
{
    public string FullName { get; init; } = string.Empty;
    public Stream? AvatarContent { get; init; }
    public long AvatarLength { get; init; }
    public string? AvatarFileName { get; init; }
    public string? AvatarContentType { get; init; }
}
