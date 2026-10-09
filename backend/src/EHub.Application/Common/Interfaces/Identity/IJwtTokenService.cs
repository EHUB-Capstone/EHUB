using System.Collections.Generic;
using EHub.Application.Common.Models.Identity;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Identity;

public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(User user, IReadOnlyCollection<string> roles);
}
