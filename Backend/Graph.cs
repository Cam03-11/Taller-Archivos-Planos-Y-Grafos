namespace DeliveryRoutes.Backend;

public class Graph<T> where T : notnull
{
    private readonly Dictionary<T, List<Edge<T>>> _adjacency = new();

    public Graph(bool directed = false)
    {
        Directed = directed;
    }

    public bool Directed { get; }

    public int VertexCount => _adjacency.Count;

    public int EdgeCount { get; private set; }

    public IEnumerable<T> Vertices => _adjacency.Keys;

    public bool AddVertex(T vertex) => _adjacency.TryAdd(vertex, new List<Edge<T>>());

    public bool ContainsVertex(T vertex) => _adjacency.ContainsKey(vertex);

    public void AddEdge(T from, T to, double weight)
    {
        AddVertex(from);
        AddVertex(to);
        _adjacency[from].Add(new Edge<T>(from, to, weight));
        EdgeCount++;

        if (!Directed)
        {
            _adjacency[to].Add(new Edge<T>(to, from, weight));
            EdgeCount++;
        }
    }

    public IReadOnlyList<Edge<T>> GetNeighbors(T vertex) =>
        _adjacency.TryGetValue(vertex, out var edges) ? edges : Array.Empty<Edge<T>>();
}
