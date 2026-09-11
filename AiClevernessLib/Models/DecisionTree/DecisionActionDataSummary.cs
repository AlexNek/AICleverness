using System.Collections.ObjectModel;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Bounded diagnostic information about data produced by a decision action.</summary>
public sealed record DecisionActionDataSummary(
    int ItemCount,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> ContentPreviews)
{
    private const int MaxPreviewItems = 5;
    private const int MaxPreviewLength = 80;
    private const int MaxTypeItems = 10;
    private const int MaxTypeLength = 80;

    /// <summary>Creates a bounded summary of produced decision data.</summary>
    public static DecisionActionDataSummary? Create(IReadOnlyList<DecisionData>? data)
    {
        if (data is null || data.Count == 0)
            return null;

        var types = new List<string>(Math.Min(data.Count, MaxTypeItems));
        var distinctTypes = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < data.Count && types.Count < MaxTypeItems; index++)
        {
            var type = data[index].Type;
            if (distinctTypes.Add(type))
                types.Add(Truncate(type, MaxTypeLength));
        }

        var previews = new List<string>(Math.Min(data.Count, MaxPreviewItems));
        for (var index = 0; index < data.Count && previews.Count < MaxPreviewItems; index++)
            previews.Add(Truncate(data[index].Content, MaxPreviewLength));

        return new DecisionActionDataSummary(
            data.Count,
            new ReadOnlyCollection<string>(types),
            new ReadOnlyCollection<string>(previews));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
