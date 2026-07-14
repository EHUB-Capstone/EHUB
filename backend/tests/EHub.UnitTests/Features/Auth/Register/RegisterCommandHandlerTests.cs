using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Domain.Common;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using EHub.Application.Common.Models.Identity;
using EHub.Application.Features.Auth;

namespace EHub.UnitTests.Features.Auth.Register;

public class RegisterCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roleRepository = Substitute.For<IRoleRepository>();
    private readonly IUserRoleRepository _userRoleRepository = Substitute.For<IUserRoleRepository>();
    private readonly IStudentRepository _studentRepository = Substitute.For<IStudentRepository>();
    private readonly IMentorProfileRepository _mentorProfileRepository = Substitute.For<IMentorProfileRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();

    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _handler = new RegisterCommandHandler(
            _userRepository,
            _roleRepository,
            _userRoleRepository,
            _studentRepository,
            _mentorProfileRepository,
            _refreshTokenRepository,
            _unitOfWork,
            _passwordHasher,
            _jwtTokenService,
            _refreshTokenService);

        // Setup common UnitOfWork mocks to execute actions directly
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                var action = x.Arg<Func<CancellationToken, Task>>();
                var ct = x.Arg<CancellationToken>();
                return action?.Invoke(ct) ?? Task.CompletedTask;
            });
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        var property = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        property?.SetValue(entity, id);
    }

    [Fact]
    public async Task Should_Have_Error_When_Register_With_Invalid_Role()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Admin",
            MajorCode = MajorCodes.BIT_SE
        };

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.InvalidRole.Code, result.Error.Code);
    }

    [Fact]
    public async Task Should_Have_Error_When_Student_Register_With_Duplicate_Email()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        _userRepository.ExistsByEmailAsync("student@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.EmailAlreadyExists.Code, result.Error.Code);
    }

    [Fact]
    public async Task Should_Register_Student_Successfully_With_Token()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        var roleId = Guid.NewGuid();
        var role = new Role { Name = SystemRoles.Student };
        SetId(role, roleId);

        _userRepository.ExistsByEmailAsync("student@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(false);
        _roleRepository.GetByNameAsync(SystemRoles.Student, Arg.Any<CancellationToken>()).Returns(role);
        _passwordHasher.Hash("Password123").Returns("hashed_password");
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<string[]>()).Returns(new AccessTokenResult
        {
            Token = "access_token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
        _refreshTokenService.GenerateRefreshToken().Returns(new RefreshTokenResult
        {
            RawToken = "raw_refresh_token",
            TokenHash = "token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.AccessToken);
        Assert.NotNull(result.Value.RefreshToken);
        Assert.Equal(UserStatus.Active.ToString(), result.Value.Status);
        Assert.False(result.Value.RequiresApproval);
        Assert.Equal(MajorCodes.BIT_SE, result.Value.User?.MajorCode);

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userRoleRepository.Received(1).AddAsync(Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
        await _studentRepository.Received(1).AddAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Register_Lecturer_Successfully_Without_Token()
    {
        var request = new RegisterRequest
        {
            FullName = "Tran Van B",
            Email = "lecturer@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Lecturer,
            MajorCode = null
        };

        var roleId = Guid.NewGuid();
        var role = new Role { Name = SystemRoles.Lecturer };
        SetId(role, roleId);

        _userRepository.ExistsByEmailAsync("lecturer@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(false);
        _roleRepository.GetByNameAsync(SystemRoles.Lecturer, Arg.Any<CancellationToken>()).Returns(role);
        _passwordHasher.Hash("Password123").Returns("hashed_password");

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AccessToken);
        Assert.Null(result.Value.RefreshToken);
        Assert.Equal(UserStatus.PendingApproval.ToString(), result.Value.Status);
        Assert.True(result.Value.RequiresApproval);

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userRoleRepository.Received(1).AddAsync(Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
        await _studentRepository.DidNotReceive().AddAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>());
        await _refreshTokenRepository.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Register_Mentor_Successfully_Without_Token_But_With_MentorProfile()
    {
        var request = new RegisterRequest
        {
            FullName = "Le Van C",
            Email = "mentor@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Mentor,
            MajorCode = null
        };

        var roleId = Guid.NewGuid();
        var role = new Role { Name = SystemRoles.Mentor };
        SetId(role, roleId);

        _userRepository.ExistsByEmailAsync("mentor@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(false);
        _roleRepository.GetByNameAsync(SystemRoles.Mentor, Arg.Any<CancellationToken>()).Returns(role);
        _passwordHasher.Hash("Password123").Returns("hashed_password");

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AccessToken);
        Assert.Null(result.Value.RefreshToken);
        Assert.Equal(UserStatus.PendingApproval.ToString(), result.Value.Status);
        Assert.True(result.Value.RequiresApproval);

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userRoleRepository.Received(1).AddAsync(Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
        await _mentorProfileRepository.Received(1).AddAsync(Arg.Any<MentorProfile>(), Arg.Any<CancellationToken>());
        await _studentRepository.DidNotReceive().AddAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
