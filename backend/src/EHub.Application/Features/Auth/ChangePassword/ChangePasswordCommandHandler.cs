using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Auth;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.ChangePassword;

public sealed class ChangePasswordCommandHandler : IChangePasswordCommandHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure(CommonErrors.Unauthorized);
        }

        var user = await _userRepository.GetByIdWithRolesAsync(
            _currentUserService.UserId.Value,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure(CommonErrors.Unauthorized);
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(AuthErrors.CurrentPasswordInvalid);
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
