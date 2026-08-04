using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.ManageSemester;

public sealed class CurrentSemesterHandler(IApplicationDbContext context, ICurrentUserService currentUser) : ICurrentSemesterHandler
{
    public async Task<Result<CurrentSemesterResponse>> GetAsync(CancellationToken token = default)
    {
        var now = DateTime.UtcNow;
        var semester = await context.Semesters
            .AsNoTracking()
            .Where(item => item.Status == SemesterStatus.Active)
            .OrderByDescending(item => item.Year)
            .ThenBy(item => item.Term)
            .FirstOrDefaultAsync(token);

        return Result.Success(new CurrentSemesterResponse
        {
            CurrentSemester = semester is null
                ? new SemesterResponse { Semester = "SP", Year = now.Year }
                : ToResponse(semester),
            AvailableYears = await GetAvailableYearsAsync(now, token),
            IsDecember = now.Month == 12,
        });
    }

    public async Task<Result<CurrentSemesterResponse>> SetAsync(SetCurrentSemesterRequest request, CancellationToken token = default)
    {
        if (!TryParseTerm(request.Semester, out var term))
        {
            return Failure("VALIDATION_ERROR", "Semester must be SP, SU, or FA.");
        }

        var now = DateTime.UtcNow;
        if (request.Year > now.Year && !(now.Month == 12 && request.Year == now.Year + 1))
        {
            return Failure("BUSINESS_RULE", "Next-year planning is only available in December.");
        }

        var semester = await context.Semesters.FirstOrDefaultAsync(
            item => item.Term == term && item.Year == request.Year,
            token);
        if (semester is null)
        {
            semester = new Semester
            {
                Code = $"{request.Semester.Trim().ToUpperInvariant()}{request.Year}",
                Name = $"{GetTermName(term)} {request.Year}",
                Term = term,
                Year = request.Year,
                Status = SemesterStatus.Planned,
                CreatedBy = currentUser.UserId,
            };

            await context.Semesters.AddAsync(semester, token);
        }

        var activeSemesters = await context.Semesters
            .Where(item => item.Status == SemesterStatus.Active && item.Id != semester.Id)
            .ToListAsync(token);
        foreach (var activeSemester in activeSemesters)
        {
            activeSemester.Status = SemesterStatus.Planned;
            activeSemester.UpdatedBy = currentUser.UserId;
        }

        semester.Status = SemesterStatus.Active;
        semester.UpdatedBy = currentUser.UserId;
        await context.SaveChangesAsync(token);

        return Result.Success(new CurrentSemesterResponse
        {
            CurrentSemester = ToResponse(semester),
            AvailableYears = await GetAvailableYearsAsync(now, token),
            IsDecember = now.Month == 12,
        });
    }

    private async Task<int[]> GetAvailableYearsAsync(DateTime now, CancellationToken token)
    {
        var years = await context.Semesters
            .AsNoTracking()
            .Select(item => item.Year)
            .Distinct()
            .ToListAsync(token);

        if (!years.Contains(now.Year))
        {
            years.Add(now.Year);
        }

        if (now.Month == 12 && !years.Contains(now.Year + 1))
        {
            years.Add(now.Year + 1);
        }

        return years.OrderByDescending(item => item).ToArray();
    }

    private static bool TryParseTerm(string? value, out SemesterTerm term)
    {
        term = value?.Trim().ToUpperInvariant() switch
        {
            "SP" => SemesterTerm.Spring,
            "SU" => SemesterTerm.Summer,
            "FA" => SemesterTerm.Fall,
            _ => default,
        };

        return value is not null && term is SemesterTerm.Spring or SemesterTerm.Summer or SemesterTerm.Fall;
    }

    private static string GetTermName(SemesterTerm term) => term switch
    {
        SemesterTerm.Spring => "Spring",
        SemesterTerm.Summer => "Summer",
        SemesterTerm.Fall => "Fall",
        _ => throw new ArgumentOutOfRangeException(nameof(term)),
    };

    private static SemesterResponse ToResponse(Semester value) => new()
    {
        Semester = value.Term switch
        {
            SemesterTerm.Spring => "SP",
            SemesterTerm.Summer => "SU",
            SemesterTerm.Fall => "FA",
            _ => throw new ArgumentOutOfRangeException(nameof(value.Term)),
        },
        Year = value.Year,
    };

    private static Result<CurrentSemesterResponse> Failure(string code, string message) =>
        Result.Failure<CurrentSemesterResponse>(new Error(code, message));
}
