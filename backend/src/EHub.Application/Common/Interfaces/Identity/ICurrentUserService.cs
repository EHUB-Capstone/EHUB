using System;
using System.Collections.Generic;

namespace EHub.Application.Common.Interfaces.Identity;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsAuthenticated { get; }
}
