using System;
using System.Collections.Generic;
using System.Linq;
using EHub.Application.Features.Showcase.Discovery;
using EHub.Contracts.Showcase;
using Xunit;

namespace EHub.UnitTests.Validators.Showcase;

public sealed class GetShowcasesRequestValidatorMatrixTests
{
    private const int ExpectedAdditionalTestCases = 404;

    private static readonly Guid KnownSemesterId =
        Guid.Parse("3f4f3dc1-c9db-4381-8ae9-626b30f25a64");

    private readonly GetShowcasesRequestValidator _validator = new();

    public static IEnumerable<object?[]> SupportedQueryCombinations()
    {
        string?[] stages = [null, "Idea", "mvp", "Beta", "Launched", " idea ", "MVP"];
        string?[] periods = [null, "topThisWeek", "thisWeek", "lastWeek", "earlier", "trending"];
        var sorts = new[] { "newest", "POPULAR", " trending " };
        var pages = new[] { 1, 2, 50, int.MaxValue / 50 };
        var pageSizes = new[] { 1, 10, 20, 50 };
        bool?[] featuredFilters = [null, true, false];
        string?[] searches = [null, string.Empty, "entrepreneurship", new string('s', 100)];
        string?[] industries = [null, string.Empty, "EdTech", new string('i', 100)];
        Guid?[] semesterIds = [null, KnownSemesterId];

        for (var caseNumber = 0; caseNumber < ExpectedAdditionalTestCases; caseNumber++)
        {
            yield return
            [
                caseNumber + 1,
                stages[(caseNumber * 3) % stages.Length],
                periods[(caseNumber * 5) % periods.Length],
                sorts[(caseNumber * 2) % sorts.Length],
                pages[(caseNumber * 3) % pages.Length],
                pageSizes[(caseNumber * 3) % pageSizes.Length],
                featuredFilters[(caseNumber * 2) % featuredFilters.Length],
                searches[(caseNumber * 3) % searches.Length],
                industries[caseNumber % industries.Length],
                semesterIds[caseNumber % semesterIds.Length]
            ];
        }
    }

    [Theory]
    [MemberData(nameof(SupportedQueryCombinations))]
    public void Validate_ShouldAccept_SupportedShowcaseQueryCombination(
        int caseNumber,
        string? stage,
        string? period,
        string sort,
        int page,
        int pageSize,
        bool? featured,
        string? search,
        string? industry,
        Guid? semesterId)
    {
        var request = new GetShowcasesRequest
        {
            Stage = stage,
            Period = period,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
            Featured = featured,
            Search = search,
            Industry = industry,
            SemesterId = semesterId
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid, $"Query combination {caseNumber} should be valid: {string.Join("; ", result.Errors.Select(error => error.ErrorMessage))}");
    }
}
