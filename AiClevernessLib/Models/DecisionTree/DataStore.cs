namespace AiCleverness.Models.DecisionTree;

/// <summary>Execution-scoped store for generic decision data.</summary>
public sealed class DataStore
{
    private readonly object _gate = new();
    private readonly List<DecisionData> _items = [];

    public void Add(DecisionData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_gate)
        {
            _items.Add(data);
        }
    }

    public IReadOnlyList<DecisionData> GetAll()
    {
        lock (_gate)
        {
            return _items.ToArray();
        }
    }

    public IReadOnlyList<DecisionData> GetByType(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        lock (_gate)
        {
            return _items.Where(item => string.Equals(item.Type, type, StringComparison.Ordinal)).ToArray();
        }
    }

    public DecisionData? GetById(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            return _items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        }
    }
}
