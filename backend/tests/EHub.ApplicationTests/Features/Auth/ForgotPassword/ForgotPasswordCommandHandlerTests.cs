using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using Microsoft.Extensions.Options;
using EHub.Application.Features.Auth.ForgotPassword;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Results;

namespace EHub.ApplicationTests.Features.Auth.ForgotPassword;

public class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordResetTokenService _passwordResetTokenService = Substitute.For<IPasswordResetTokenService>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IOptions<FrontendOptions> _frontendOptions = Substitute.For<IOptions<FrontendOptions>>();
    private readonly IOptions<PasswordResetOptions> _passwordResetOptions = Substitute.For<IOptions<PasswordResetOptions>>();

    private readonly ForgotPasswordCommandHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        _frontendOptions.Value.Returns(new FrontendOptions { BaseUrl = "http://localhost:3000" });
        _passwordResetOptions.Value.Returns(new PasswordResetOptions { TokenExpirationMinutes = 15 });

        _handler = new ForgotPasswordCommandHandler(
            _userRepository,
            _passwordResetTokenRepository,
            _passwordResetTokenService,
            _emailService,
            _dateTimeProvider,
            _unitOfWork,
            _frontendOptions,
            _passwordResetOptions);
    }

    [Fact]
    public async Task Should_Return_Success_And_Do_Nothing_When_User_Not_Found()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "notfound@example.com" };
        _userRepository.GetByEmailWithRolesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.HandleAsync(request, "127.0.0.1", "Mozilla", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _passwordResetTokenRepository.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
        await _emailService.DidNotReceive().SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Create_Token_And_Send_Email_When_User_Exists()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "student@example.com" };
        var user = new User
        {
            FullName = "John Student",
            Email = "student@example.com",
            Status = UserStatus.Active
        };
        
        _userRepository.GetByEmailWithRolesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _passwordResetTokenService.GenerateRawToken().Returns("raw-token");
        _passwordResetTokenService.HashToken("raw-token").Returns("hashed-token");

        // Act
        var result = await _handler.HandleAsync(request, "127.0.0.1", "Mozilla", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _passwordResetTokenRepository.Received(1).MarkActiveTokensAsUsedByUserIdAsync(user.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _passwordResetTokenRepository.Received(1).AddAsync(Arg.Is<PasswordResetToken>(t => t != null && t.UserId == user.Id && t.TokenHash == "hashed-token"), Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendPasswordResetEmailAsync(user.Email!, user.FullName, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
