using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.StartupIndustries;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.StartupIndustries.ManageStartupIndustries;

public sealed class StartupIndustryManagementHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IStartupIndustryManagementHandler
{
    public async Task<Result<StartupIndustryListResponse>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var industries = await context.StartupIndustries
            .AsNoTracking()
            .Where(industry => industry.Status == StartupIndustryStatus.Active)
            .OrderBy(industry => industry.Name)
            .ToArrayAsync(cancellationToken);

        return Result.Success(new StartupIndustryListResponse
        {
            Industries = industries.Select(ToResponse).ToArray()
        });
    }

    public async Task<Result<StartupIndustryListResponse>> GetAsync(
        string? search,
        string? status,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseStatus(status, out var parsedStatus))
        {
            return Fail<StartupIndustryListResponse>("VALIDATION_ERROR", "Status must be active or inactive.");
        }

        if (!TryNormalizeSort(sort, out var normalizedSort))
        {
            return Fail<StartupIndustryListResponse>("VALIDATION_ERROR", "Sort must be name-asc or name-desc.");
        }

        var query = context.StartupIndustries
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(industry =>
                industry.NormalizedName.Contains(term));
        }

        if (parsedStatus is not null)
        {
            query = query.Where(industry => industry.Status == parsedStatus.Value);
        }

        query = normalizedSort == "name-desc"
            ? query.OrderByDescending(industry => industry.Name)
            : query.OrderBy(industry => industry.Name);

        var industries = await query.ToArrayAsync(cancellationToken);
        return Result.Success(new StartupIndustryListResponse
        {
            Industries = industries.Select(ToResponse).ToArray()
        });
    }

    public async Task<Result<StartupIndustryResponse>> CreateAsync(
        CreateStartupIndustryRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var normalizedName = NormalizeName(name);
        if (await context.StartupIndustries.AnyAsync(industry => industry.NormalizedName == normalizedName, cancellationToken))
        {
            return Fail<StartupIndustryResponse>("STARTUP_INDUSTRY_NAME_EXISTS", "An industry with this name already exists.");
        }

        var industry = new StartupIndustry
        {
            Name = name,
            NormalizedName = normalizedName,
            Description = NormalizeDescription(request.Description),
            Status = ParseStatus(request.Status),
            CreatedBy = currentUser.UserId
        };

        await context.StartupIndustries.AddAsync(industry, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(industry));
    }

    public async Task<Result<StartupIndustryResponse>> UpdateAsync(
        Guid id,
        UpdateStartupIndustryRequest request,
        CancellationToken cancellationToken = default)
    {
        var industry = await context.StartupIndustries
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (industry is null)
        {
            return Fail<StartupIndustryResponse>("STARTUP_INDUSTRY_NOT_FOUND", "Startup industry was not found.");
        }

        var name = request.Name.Trim();
        var normalizedName = NormalizeName(name);
        if (await context.StartupIndustries.AnyAsync(
                item => item.Id != id && item.NormalizedName == normalizedName,
                cancellationToken))
        {
            return Fail<StartupIndustryResponse>("STARTUP_INDUSTRY_NAME_EXISTS", "An industry with this name already exists.");
        }

        industry.Name = name;
        industry.NormalizedName = normalizedName;
        industry.Description = NormalizeDescription(request.Description);
        industry.Status = ParseStatus(request.Status);
        industry.UpdatedBy = currentUser.UserId;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(industry));
    }

    public async Task<Result<StartupIndustryResponse>> ChangeStatusAsync(
        Guid id,
        ChangeStartupIndustryStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var industry = await context.StartupIndustries
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (industry is null)
        {
            return Fail<StartupIndustryResponse>("STARTUP_INDUSTRY_NOT_FOUND", "Startup industry was not found.");
        }

        industry.Status = ParseStatus(request.Status);
        industry.UpdatedBy = currentUser.UserId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(industry));
    }

    private static string NormalizeName(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeDescription(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseStatus(string? status, out StartupIndustryStatus? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(status)) return true;
        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            value = StartupIndustryStatus.Active;
            return true;
        }

        if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
        {
            value = StartupIndustryStatus.Inactive;
            return true;
        }

        return false;
    }

    private static StartupIndustryStatus ParseStatus(string status) =>
        status.Equals("inactive", StringComparison.OrdinalIgnoreCase)
            ? StartupIndustryStatus.Inactive
            : StartupIndustryStatus.Active;

    private static bool TryNormalizeSort(string? sort, out string normalized)
    {
        normalized = string.IsNullOrWhiteSpace(sort) ? "name-asc" : sort.Trim().ToLowerInvariant();
        return normalized is "name-asc" or "name-desc";
    }

    private static StartupIndustryResponse ToResponse(StartupIndustry industry) => new()
    {
        Id = industry.Id,
        Name = industry.Name,
        Description = industry.Description,
        Status = industry.Status == StartupIndustryStatus.Active ? "active" : "inactive"
    };

    private static Result<T> Fail<T>(string code, string message) =>
        Result.Failure<T>(new Error(code, message));
}
