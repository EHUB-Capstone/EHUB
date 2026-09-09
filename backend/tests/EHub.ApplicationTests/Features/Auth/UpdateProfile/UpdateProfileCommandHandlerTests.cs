using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Auth.UpdateProfile;
using EHub.Domain.Common;
using EHub.Domain.Entities;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Auth.UpdateProfile;

public sealed class UpdateProfileCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
    private readonly IImageStorageService _images = Substitute.For<IImageStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdateProfileCommandHandler _handler;

    public UpdateProfileCommandHandlerTests()
    {
        _handler = new UpdateProfileCommandHandler(_currentUser, _users, _students, _images, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotAuthenticated()
    {
        _currentUser.IsAuthenticated.Returns(false);

        var result = await _handler.HandleAsync(new UpdateProfileCommand { FullName = "Nguyen Van A" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.CommonUnauthorizedError, result.Error.Code);
        await _users.DidNotReceiveWithAnyArgs().GetByIdWithRolesAsync(default, default);
    }

    [Fact]
    public async Task HandleAsync_RejectsAvatarWithInvalidSignature()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid());

        await using var content = new MemoryStream([1, 2, 3, 4]);
        var result = await _handler.HandleAsync(new UpdateProfileCommand
        {
            FullName = "Nguyen Van A",
            AvatarContent = content,
            AvatarLength = content.Length,
            AvatarFileName = "avatar.png",
            AvatarContentType = "image/png"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthProfileImageInvalid, result.Error.Code);
        await _images.DidNotReceiveWithAnyArgs().UploadAvatarAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task HandleAsync_UpdatesNameAndAvatar_WhenUploadSucceeds()
    {
        var userId = Guid.NewGuid();
        var user = new User { FullName = "Old name", AvatarUrl = "https://old.example/avatar.png" };
        SetId(user, userId);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _users.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _students.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns((Student?)null);
        _images.UploadAvatarAsync(Arg.Any<Stream>(), "avatar.png", "image/png", userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new ImageUploadResult("https://cdn.example/avatar.png"))));

        await using var content = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var result = await _handler.HandleAsync(new UpdateProfileCommand
        {
            FullName = "  New name  ",
            AvatarContent = content,
            AvatarLength = content.Length,
            AvatarFileName = "avatar.png",
            AvatarContentType = "image/png"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", user.FullName);
        Assert.Equal("https://cdn.example/avatar.png", user.AvatarUrl);
        Assert.Equal("New name", result.Value.FullName);
        _users.Received(1).Update(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)?.SetValue(entity, id);
    }
}
