namespace EHub.Application.Features.ProjectData.Common;

/// <summary>Case-insensitive comparer that compares runs of digits by numeric value (G2 comes before G10).</summary>
internal sealed class NaturalStringComparer : IComparer<string>
{
    internal static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var i = 0;
        var j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                var numberX = x[startX..i].TrimStart('0');
                var numberY = y[startY..j].TrimStart('0');
                if (numberX.Length != numberY.Length) return numberX.Length.CompareTo(numberY.Length);
                var byValue = string.CompareOrdinal(numberX, numberY);
                if (byValue != 0) return byValue;
            }
            else
            {
                var byChar = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (byChar != 0) return byChar;
                i++;
                j++;
            }
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
