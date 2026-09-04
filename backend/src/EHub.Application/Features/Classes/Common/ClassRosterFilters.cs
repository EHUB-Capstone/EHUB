using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Classes.Common;

internal static class ClassRosterFilters
{
    public static bool TryParseStatus(string? value, out EnrollmentStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Enum.TryParse<EnrollmentStatus>(value, true, out var parsed))
        {
            return false;
        }

        status = parsed;
        return true;
    }

    public static IQueryable<ClassStudent> Apply(
        IQueryable<ClassStudent> query,
        string? search,
        string? majorCode,
        EnrollmentStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            if (normalizedSearch.Length > 100)
            {
                normalizedSearch = normalizedSearch[..100];
            }

            query = query.Where(enrollment =>
                (enrollment.Student.RollNumber != null && enrollment.Student.RollNumber.ToLower().Contains(normalizedSearch)) ||
                enrollment.Student.FullName.ToLower().Contains(normalizedSearch) ||
                (enrollment.Student.Email != null && enrollment.Student.Email.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(majorCode))
        {
            var normalizedMajor = majorCode.Trim().ToUpperInvariant();
            query = query.Where(enrollment =>
                enrollment.MajorCodeAtEnrollment.ToUpper() == normalizedMajor ||
                (
                    (
                        enrollment.MajorCodeAtEnrollment == null ||
                        enrollment.MajorCodeAtEnrollment == string.Empty ||
                        enrollment.MajorCodeAtEnrollment.ToUpper() == MajorCodes.Undeclared
                    ) &&
                    enrollment.Student.MajorCode != null &&
                    enrollment.Student.MajorCode.ToUpper() == normalizedMajor
                ));
        }

        if (status.HasValue)
        {
            query = query.Where(enrollment => enrollment.EnrollmentStatus == status.Value);
        }

        return query;
    }
}
