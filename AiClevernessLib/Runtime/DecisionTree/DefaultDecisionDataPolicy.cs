using System.Collections.ObjectModel;
using System.Globalization;

using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Deterministically bounds decision data before prompt construction.</summary>
public sealed class DefaultDecisionDataPolicy : IDecisionDataPolicy
{
    private const string MarkerId = "decision-context-policy";
    private const string MarkerSource = "AICleverness";
    private const string MarkerType = "selection";

    // Brackets, labels, and separators in the canonical representation.
    private const int CanonicalRepresentationFixedCharacterCount = 11;

    private readonly DecisionDataPolicyOptions _options;

    public DefaultDecisionDataPolicy(DecisionDataPolicyOptions? options = null)
    {
        _options = options ?? new DecisionDataPolicyOptions();
        _options.Validate();
    }

    public DecisionDataSelection Select(
        IReadOnlyList<DecisionData> data,
        DecisionDataSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Tree);
        ArgumentNullException.ThrowIfNull(context.ClassifyNode);
        ArgumentNullException.ThrowIfNull(context.State);
        ArgumentNullException.ThrowIfNull(context.TemplateParameters);

        var selected = new List<DecisionData>(Math.Min(data.Count, _options.MaxItems));
        var omitted = 0;
        var perItemTruncated = 0;
        var aggregateTruncated = 0;
        var aggregateLength = 0;

        foreach (var item in data)
        {
            if (!IsIncluded(item))
                continue;

            if (selected.Count >= _options.MaxItems)
            {
                omitted++;
                continue;
            }

            var displayId = Limit(item.Id, _options.MaxFieldLength, "[id truncated]");
            var displaySource = Limit(item.Source, _options.MaxFieldLength, "[source truncated]");
            var displayType = Limit(item.Type, _options.MaxFieldLength, "[type truncated]");
            var boundedMetadata = LimitMetadata(item.Metadata);
            var representedItem = item with
            {
                ActionId = LimitNullable(item.ActionId, _options.MaxFieldLength, "[action truncated]"),
                Metadata = boundedMetadata,
                DisplayId = string.Equals(displayId, item.Id, StringComparison.Ordinal) ? null : displayId,
                DisplaySource = string.Equals(displaySource, item.Source, StringComparison.Ordinal) ? null : displaySource,
                DisplayType = string.Equals(displayType, item.Type, StringComparison.Ordinal) ? null : displayType
            };
            var separatorLength = selected.Count == 0 ? 0 : 2;
            var fixedLength = CanonicalRepresentationLength(representedItem, string.Empty);
            var remaining = _options.MaxAggregateRepresentationLength - aggregateLength - separatorLength - fixedLength;
            if (remaining < 0)
            {
                omitted++;
                continue;
            }

            var contentLimit = Math.Min(_options.MaxContentLengthPerItem, remaining);
            var content = Limit(item.Content, contentLimit, "[content truncated]");
            if (item.Content.Length > _options.MaxContentLengthPerItem)
                perItemTruncated++;
            if (item.Content.Length > contentLimit && contentLimit < _options.MaxContentLengthPerItem)
                aggregateTruncated++;

            representedItem = representedItem with { Content = content };
            aggregateLength += separatorLength + CanonicalRepresentationLength(representedItem, content);
            selected.Add(representedItem);
        }

        if (omitted > 0 || perItemTruncated > 0 || aggregateTruncated > 0)
        {
            selected.Add(
                new DecisionData
                {
                    Id = MarkerId,
                    Source = Limit(MarkerSource, _options.MaxFieldLength, "[source truncated]"),
                    Type = Limit(MarkerType, _options.MaxFieldLength, "[type truncated]"),
                    Content = CreateMarkerContent(
                        omitted,
                        perItemTruncated,
                        aggregateTruncated,
                        _options.MaxContentLengthPerItem)
                });
        }

        return new DecisionDataSelection(
            selected,
            omitted,
            perItemTruncated,
            aggregateTruncated);
    }

    private bool IsIncluded(DecisionData item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return (_options.IncludedTypes is null || _options.IncludedTypes.Contains(item.Type))
            && (_options.IncludedSources is null || _options.IncludedSources.Contains(item.Source));
    }

    private IReadOnlyDictionary<string, string>? LimitMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
            return null;

        var entries = metadata.ToArray();
        var omitted = Math.Max(0, entries.Length - _options.MaxMetadataEntries);
        var dataEntryLimit = omitted > 0
            ? Math.Max(0, _options.MaxMetadataEntries - 1)
            : _options.MaxMetadataEntries;
        var bounded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in entries.Take(dataEntryLimit))
        {
            var key = Limit(pair.Key, _options.MaxMetadataKeyLength, "[metadata key truncated]");
            var value = Limit(pair.Value, _options.MaxMetadataValueLength, "[metadata value truncated]");
            if (!bounded.ContainsKey(key))
                bounded.Add(key, value);
        }

        if (omitted > 0)
        {
            var markerKey = Limit("[metadata entries omitted]", _options.MaxMetadataKeyLength, "[metadata omitted]");
            bounded[markerKey] = omitted.ToString(CultureInfo.InvariantCulture);
        }

        return new ReadOnlyDictionary<string, string>(bounded);
    }

    private static int CanonicalRepresentationLength(DecisionData item, string content)
    {
        var id = item.DisplayId ?? item.Id;
        var source = item.DisplaySource ?? item.Source;
        var type = item.DisplayType ?? item.Type;
        var metadataLength = item.Metadata?.Sum(pair => pair.Key.Length + 1 + pair.Value.Length + 2) ?? 0;
        return id.Length + type.Length + source.Length + content.Length + metadataLength + CanonicalRepresentationFixedCharacterCount;
    }

    private static string CreateMarkerContent(
        int omitted,
        int perItemTruncated,
        int aggregateTruncated,
        int maximum)
    {
        var content = $"Decision data policy: omitted {omitted} item(s); truncated {perItemTruncated} item(s); aggregate-truncated {aggregateTruncated} item(s).";
        if (content.Length <= maximum)
            return content;

        var compactContent = omitted > 0
            ? $"omitted {omitted}; t{perItemTruncated}; a{aggregateTruncated}"
            : perItemTruncated > 0
                ? $"truncated {perItemTruncated}; o{omitted}; a{aggregateTruncated}"
                : $"aggregate-truncated {aggregateTruncated}; o{omitted}; t{perItemTruncated}";
        if (compactContent.Length <= maximum)
            return compactContent;

        return Limit($"o{omitted};t{perItemTruncated};a{aggregateTruncated}", maximum, "[content truncated]");
    }

    private static string? LimitNullable(string? value, int maximum, string marker)
        => value is null ? null : Limit(value, maximum, marker);

    private static string Limit(string value, int maximum, string marker)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (maximum <= 0)
            return string.Empty;
        if (value.Length <= maximum)
            return value;
        if (maximum <= marker.Length)
            return marker[..maximum];
        return value[..(maximum - marker.Length)] + marker;
    }
}
