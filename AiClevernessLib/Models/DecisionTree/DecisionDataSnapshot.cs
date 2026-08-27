namespace AiCleverness.Models.DecisionTree;

/// <summary>Read-only decision-data representation supplied to a classification context builder.</summary>
public sealed class DecisionDataSnapshot
{
    private readonly IReadOnlyList<DecisionData> _items;

    public DecisionDataSnapshot(IEnumerable<DecisionData> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = Array.AsReadOnly(items.ToArray());
    }

    /// <summary>Returns the selected representation items without exposing mutation operations.</summary>
    public IReadOnlyList<DecisionData> GetAll() => _items;

    /// <summary>Returns represented items with the specified type.</summary>
    public IReadOnlyList<DecisionData> GetByType(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _items
            .Where(item => string.Equals(item.Type, type, StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>Returns the represented item with the specified stable identifier.</summary>
    public DecisionData? GetById(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
    }
}
