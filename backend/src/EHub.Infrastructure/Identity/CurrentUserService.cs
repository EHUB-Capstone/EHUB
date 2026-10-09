using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Shared.Constants;

namespace EHub.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimNames.UserId)?.Value
                ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimNames.Email)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var claims = _httpContextAccessor.HttpContext?.User?.Claims;
            if (claims == null) return Array.Empty<string>();

            return claims
                .Where(c => c.Type == ClaimNames.Role || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
