using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Auth;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.UpdateProfile;

public sealed class UpdateProfileCommandHandler : IUpdateProfileCommandHandler
{
    private const long MaximumAvatarSize = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IImageStorageService _imageStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IImageStorageService imageStorageService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _imageStorageService = imageStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UpdateProfileResponse>> HandleAsync(
        UpdateProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<UpdateProfileResponse>(CommonErrors.Unauthorized);
        }

        var fullName = command.FullName.Trim();
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 200)
        {
            return Result.Failure<UpdateProfileResponse>(
                ErrorCodes.CommonValidationError,
                "Full name is required and must not exceed 200 characters.");
        }

        if (!IsValidAvatar(command, out var validationMessage))
        {
            return Result.Failure<UpdateProfileResponse>(
                ErrorCodes.AuthProfileImageInvalid,
                validationMessage);
        }

        var user = await _userRepository.GetByIdWithRolesAsync(
            _currentUserService.UserId.Value,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure<UpdateProfileResponse>(CommonErrors.Unauthorized);
        }

        string? avatarUrl = user.AvatarUrl;
        if (command.AvatarContent is not null)
        {
            var uploadResult = await _imageStorageService.UploadAvatarAsync(
                command.AvatarContent,
                command.AvatarFileName!,
                command.AvatarContentType!,
                user.Id,
                cancellationToken);

            if (uploadResult.IsFailure)
            {
                return Result.Failure<UpdateProfileResponse>(uploadResult.Error);
            }

            avatarUrl = uploadResult.Value.SecureUrl;
        }

        user.FullName = fullName;
        user.AvatarUrl = avatarUrl;
        _userRepository.Update(user);

        var student = await _studentRepository.GetByUserIdAsync(user.Id, cancellationToken);
        if (student is not null)
        {
            student.FullName = fullName;
            student.AvatarUrl = avatarUrl;
            _studentRepository.Update(student);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new UpdateProfileResponse
        {
            Id = user.Id,
            FullName = fullName,
            AvatarUrl = avatarUrl
        });
    }

    private static bool IsValidAvatar(UpdateProfileCommand command, out string message)
    {
        message = string.Empty;
        if (command.AvatarContent is null)
        {
            return true;
        }

        if (command.AvatarLength is <= 0 or > MaximumAvatarSize ||
            string.IsNullOrWhiteSpace(command.AvatarFileName) ||
            string.IsNullOrWhiteSpace(command.AvatarContentType) ||
            !AllowedContentTypes.Contains(command.AvatarContentType))
        {
            message = "Avatar must be a JPEG, PNG, or WebP image no larger than 5 MB.";
            return false;
        }

        if (!command.AvatarContent.CanSeek || !HasImageSignature(command.AvatarContent))
        {
            message = "Avatar content does not match a supported image format.";
            return false;
        }

        return true;
    }

    private static bool HasImageSignature(Stream content)
    {
        var originalPosition = content.Position;
        try
        {
            Span<byte> header = stackalloc byte[12];
            var read = content.Read(header);
            return (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) ||
                   (read >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) ||
                   (read >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8));
        }
        finally
        {
            content.Position = originalPosition;
        }
    }
}
