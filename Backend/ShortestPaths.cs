namespace DeliveryRoutes.Backend;

public class ShortestPaths<T> where T : notnull
{
    private readonly Dictionary<T, double> _distance;
    private readonly Dictionary<T, T> _previous;

    private ShortestPaths(T source, Dictionary<T, double> distance, Dictionary<T, T> previous)
    {
        Source = source;
        _distance = distance;
        _previous = previous;
    }

    public T Source { get; }

    public static ShortestPaths<T> Dijkstra(Graph<T> graph, T source)
    {
        if (!graph.ContainsVertex(source))
        {
            throw new ArgumentException($"Vertex '{source}' is not in the graph.", nameof(source));
        }

        var distance = new Dictionary<T, double> { [source] = 0 };
        var previous = new Dictionary<T, T>();
        var visited = new HashSet<T>();
        var queue = new PriorityQueue<T, double>();
        queue.Enqueue(source, 0);

        while (queue.TryDequeue(out var current, out var currentDistance))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var edge in graph.GetNeighbors(current))
            {
                var candidate = currentDistance + edge.Weight;
                if (!distance.TryGetValue(edge.To, out var known) || candidate < known)
                {
                    distance[edge.To] = candidate;
                    previous[edge.To] = current;
                    queue.Enqueue(edge.To, candidate);
                }
            }
        }

        return new ShortestPaths<T>(source, distance, previous);
    }

    public bool CanReach(T target) => _distance.ContainsKey(target);

    public double DistanceTo(T target) =>
        _distance.TryGetValue(target, out var d) ? d : double.PositiveInfinity;

    public List<T> PathTo(T target)
    {
        if (!CanReach(target))
        {
            return new List<T>();
        }

        var path = new List<T> { target };
        var current = target;
        while (_previous.TryGetValue(current, out var before))
        {
            path.Add(before);
            current = before;
        }

        path.Reverse();
        return path;
    }
}
