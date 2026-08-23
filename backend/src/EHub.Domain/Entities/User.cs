using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class User : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    // User entities are created only by trusted internal/admin flows or after
    // an external identity/OTP has verified the address. Public registration
    // stores an isolated PendingRegistration and does not construct a User.
    public bool IsEmailVerified { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual MentorProfile? MentorProfile { get; set; }
    public virtual Student? Student { get; set; }
}
