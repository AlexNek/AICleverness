using System.Collections.ObjectModel;

namespace AiCleverness.Models.DecisionTree;

/// <summary>Bounded diagnostic information about data produced by a decision action.</summary>
public sealed record DecisionActionDataSummary
{
    private IReadOnlyList<string> _types = Array.Empty<string>();
    private IReadOnlyList<string> _contentPreviews = Array.Empty<string>();

    public DecisionActionDataSummary(
        int itemCount,
        IReadOnlyList<string> types,
        IReadOnlyList<string> contentPreviews)
    {
        ItemCount = itemCount;
        Types = types;
        ContentPreviews = contentPreviews;
    }

    public int ItemCount { get; init; }

    public IReadOnlyList<string> Types
    {
        get => _types;
        init => _types = Copy(value);
    }

    public IReadOnlyList<string> ContentPreviews
    {
        get => _contentPreviews;
        init => _contentPreviews = Copy(value);
    }

    public void Deconstruct(
        out int itemCount,
        out IReadOnlyList<string> types,
        out IReadOnlyList<string> contentPreviews)
    {
        itemCount = ItemCount;
        types = Types;
        contentPreviews = ContentPreviews;
    }

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

        return new DecisionActionDataSummary(data.Count, types, previews);
    }

    private static IReadOnlyList<string> Copy(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<string>(values.ToArray());
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
