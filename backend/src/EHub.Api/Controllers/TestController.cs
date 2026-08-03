using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using EHub.Application.Common.Exceptions;
using EHub.Contracts.Common;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    [HttpGet("validation")]
    public IActionResult GetValidationError()
    {
        var errors = new List<ValidationError>
        {
            new() { Field = "Email", Message = "Email is required", Code = "EMAIL_REQUIRED" },
            new() { Field = "Password", Message = "Password must be at least 6 characters", Code = "PASSWORD_TOO_SHORT" }
        };
        throw new ValidationException(errors);
    }

    [HttpGet("business")]
    public IActionResult GetBusinessError()
    {
        throw new BusinessRuleException("Student is already registered in another class.");
    }

    [HttpGet("unauthorized")]
    public IActionResult GetUnauthorizedError()
    {
        throw new UnauthorizedException("Session has expired. Please log in again.");
    }

    [HttpGet("forbidden")]
    public IActionResult GetForbiddenError()
    {
        throw new ForbiddenException("You do not have access to this resource.");
    }

    [HttpGet("notfound")]
    public IActionResult GetNotFoundError()
    {
        throw new NotFoundException("Project not found.");
    }

    [HttpGet("conflict")]
    public IActionResult GetConflictError()
    {
        throw new ConflictException("Email is already in use.");
    }

    [HttpGet("unexpected")]
    public IActionResult GetUnexpectedError()
    {
        throw new Exception("Database disk full error occurred!");
    }

    [HttpPost("register")]
    public IActionResult TestRegister(EHub.Contracts.Auth.RegisterRequest request)
    {
        return Ok(ApiResponse<string>.SuccessResponse("Validation passed", "Success"));
    }

    [HttpPost("login")]
    public IActionResult TestLogin(EHub.Contracts.Auth.EmailPasswordLoginRequest request)
    {
        return Ok(ApiResponse<string>.SuccessResponse("Validation passed", "Success"));
    }

    [HttpPost("google-login")]
    public IActionResult TestGoogleLogin(EHub.Contracts.Auth.GoogleLoginRequest request)
    {
        return Ok(ApiResponse<string>.SuccessResponse("Validation passed", "Success"));
    }
}
