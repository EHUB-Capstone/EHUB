using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EHub.Api.Models.Auth;

public sealed class UpdateProfileFormRequest
{
    [Required]
    [StringLength(200)]
    public string FullName { get; init; } = string.Empty;

    public IFormFile? Avatar { get; init; }
}
