using System;
using System.Collections.Generic;

namespace EHub.Application.Features.Classes.ExportAdminClassData;

internal sealed class NaturalCodeComparer : IComparer<string?>
{
    internal static NaturalCodeComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var leftIsDigit = char.IsDigit(left[leftIndex]);
            var rightIsDigit = char.IsDigit(right[rightIndex]);
            if (leftIsDigit && rightIsDigit)
            {
                var leftEnd = ReadDigitRun(left, leftIndex);
                var rightEnd = ReadDigitRun(right, rightIndex);
                var comparison = CompareDigitRuns(
                    left.AsSpan(leftIndex, leftEnd - leftIndex),
                    right.AsSpan(rightIndex, rightEnd - rightIndex));
                if (comparison != 0) return comparison;
                leftIndex = leftEnd;
                rightIndex = rightEnd;
                continue;
            }

            var characterComparison = char.ToUpperInvariant(left[leftIndex])
                .CompareTo(char.ToUpperInvariant(right[rightIndex]));
            if (characterComparison != 0) return characterComparison;
            leftIndex++;
            rightIndex++;
        }

        var lengthComparison = (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        return lengthComparison != 0
            ? lengthComparison
            : StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }

    private static int ReadDigitRun(string value, int start)
    {
        var index = start;
        while (index < value.Length && char.IsDigit(value[index])) index++;
        return index;
    }

    private static int CompareDigitRuns(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var leftSignificant = left.TrimStart('0');
        var rightSignificant = right.TrimStart('0');
        if (leftSignificant.IsEmpty) leftSignificant = "0";
        if (rightSignificant.IsEmpty) rightSignificant = "0";

        var lengthComparison = leftSignificant.Length.CompareTo(rightSignificant.Length);
        if (lengthComparison != 0) return lengthComparison;

        var valueComparison = leftSignificant.CompareTo(rightSignificant, StringComparison.Ordinal);
        if (valueComparison != 0) return valueComparison;

        return left.Length.CompareTo(right.Length);
    }
}
