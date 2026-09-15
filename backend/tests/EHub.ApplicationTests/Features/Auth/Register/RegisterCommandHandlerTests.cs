using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using EHub.Application.Features.Auth;
using EHub.Application.Features.Auth.Register;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Shared.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Auth.Register;

public sealed class RegisterCommandHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 8, 23, 8, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roleRepository = Substitute.For<IRoleRepository>();
    private readonly IPendingRegistrationRepository _pendingRegistrationRepository =
        Substitute.For<IPendingRegistrationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IRegistrationOtpService _otpService = Substitute.For<IRegistrationOtpService>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var options = Options.Create(new RegistrationOtpOptions
        {
            ExpirationMinutes = 5,
            MaximumAttempts = 5,
            ResendCooldownSeconds = 60,
            MaximumResends = 5
        });

        _dateTimeProvider.UtcNow.Returns(UtcNow);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        _otpService.GenerateCode().Returns("123456");
        _otpService.HashCode(Arg.Any<Guid>(), "123456").Returns("hashed-otp");

        _handler = new RegisterCommandHandler(
            _userRepository,
            _roleRepository,
            _pendingRegistrationRepository,
            _unitOfWork,
            _passwordHasher,
            _otpService,
            _emailService,
            _dateTimeProvider,
            options,
            Substitute.For<ILogger<RegisterCommandHandler>>());
    }

    [Fact]
    public async Task HandleAsync_WithValidStudent_CreatesPendingRegistrationAndSendsOtp()
    {
        var request = CreateStudentRequest();
        _roleRepository.GetByNameAsync(SystemRoles.Student, Arg.Any<CancellationToken>())
            .Returns(new Role { Name = SystemRoles.Student });

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.RequiresEmailVerification);
        Assert.False(result.Value.RequiresApproval);
        Assert.Equal("PendingEmailVerification", result.Value.Status);
        Assert.Null(result.Value.AccessToken);
        Assert.Null(result.Value.RefreshToken);
        Assert.Equal(UtcNow.AddMinutes(5), result.Value.VerificationExpiresAtUtc);

        await _pendingRegistrationRepository.Received(1).AddAsync(
            Arg.Is<PendingRegistration>(registration =>
                registration != null &&
                registration.Email == "student@fpt.edu.vn" &&
                registration.NormalizedEmail == "student@fpt.edu.vn" &&
                registration.RoleName == SystemRoles.Student &&
                registration.MajorCode == MajorCodes.BIT_SE &&
                registration.OtpHash == "hashed-otp"),
            Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendRegistrationOtpAsync(
            "student@fpt.edu.vn",
            "Nguyen Van A",
            "123456",
            UtcNow.AddMinutes(5),
            Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenUserAlreadyExists_ReturnsEmailAlreadyExists()
    {
        _userRepository.ExistsByEmailAsync(
                "student@fpt.edu.vn",
                Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.HandleAsync(CreateStudentRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.EmailAlreadyExists.Code, result.Error.Code);
        await _emailService.DidNotReceiveWithAnyArgs().SendRegistrationOtpAsync(
            default!, default!, default!, default, default);
    }

    [Fact]
    public async Task HandleAsync_WhenActiveChallengeIsCoolingDown_ReturnsResendTooSoon()
    {
        var pending = new PendingRegistration
        {
            Email = "student@fpt.edu.vn",
            NormalizedEmail = "student@fpt.edu.vn",
            PasswordHash = "hashed-password",
            RoleName = SystemRoles.Student,
            OtpHash = "old-hash",
            OtpExpiresAtUtc = UtcNow.AddMinutes(3),
            LastSentAtUtc = UtcNow.AddSeconds(-10)
        };

        _roleRepository.GetByNameAsync(SystemRoles.Student, Arg.Any<CancellationToken>())
            .Returns(new Role { Name = SystemRoles.Student });
        _pendingRegistrationRepository.GetByNormalizedEmailAsync(
                "student@fpt.edu.vn",
                Arg.Any<CancellationToken>())
            .Returns(pending);
        _passwordHasher.Verify("Password123", "hashed-password").Returns(true);

        var result = await _handler.HandleAsync(CreateStudentRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.VerificationResendTooSoon.Code, result.Error.Code);
        await _emailService.DidNotReceiveWithAnyArgs().SendRegistrationOtpAsync(
            default!, default!, default!, default, default);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidStudentMajor_ReturnsInvalidMajor()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = "UNKNOWN"
        };
        _roleRepository.GetByNameAsync(SystemRoles.Student, Arg.Any<CancellationToken>())
            .Returns(new Role { Name = SystemRoles.Student });

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.InvalidMajor.Code, result.Error.Code);
    }

    private static RegisterRequest CreateStudentRequest() => new()
    {
        FullName = "Nguyen Van A",
        Email = "student@fpt.edu.vn",
        Password = "Password123",
        ConfirmPassword = "Password123",
        Role = SystemRoles.Student,
        MajorCode = MajorCodes.BIT_SE
    };
}
