namespace EHub.Application.Features.ProposalAnalyses;

public static class CosineSimilarity
{
    public static double Calculate(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Count == 0 || left.Count != right.Count)
            throw new ArgumentException("Embedding vectors must have the same non-zero dimension.");

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var index = 0; index < left.Count; index++)
        {
            var leftValue = left[index];
            var rightValue = right[index];
            if (!float.IsFinite(leftValue) || !float.IsFinite(rightValue))
                throw new ArgumentException("Embedding vectors must contain only finite values.");
            dot += leftValue * rightValue;
            leftNorm += leftValue * leftValue;
            rightNorm += rightValue * rightValue;
        }

        if (leftNorm <= 0 || rightNorm <= 0)
            throw new ArgumentException("Embedding vectors must have a non-zero norm.");

        return Math.Clamp(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)), -1d, 1d);
    }
}
