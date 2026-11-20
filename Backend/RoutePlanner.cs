namespace DeliveryRoutes.Backend;

public class RoutePlanner
{
    private const double Epsilon = 1e-9;

    private readonly Graph<string> _city;
    private readonly IReadOnlyList<Package> _packages;
    private readonly string _start;
    private readonly Dictionary<string, ShortestPaths<string>> _paths = new();
    private readonly double[,] _distance;
    private readonly int[] _stopPoint;
    private readonly int _startPoint;

    public RoutePlanner(Graph<string> city, IReadOnlyList<Package> packages, string start)
    {
        if (!city.ContainsVertex(start))
        {
            throw new InputValidationException(
                "Posición inicial inválida:", $"La ubicación '{start}' no existe en el mapa.");
        }

        _city = city;
        _packages = packages;
        _start = start;

        var points = new List<string> { start };
        foreach (var package in packages)
        {
            points.Add(package.Pickup);
            points.Add(package.Delivery);
        }

        points = points.Distinct().ToList();
        var pointIndex = points.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index);
        _startPoint = pointIndex[start];

        _stopPoint = new int[packages.Count * 2];
        for (var k = 0; k < packages.Count; k++)
        {
            _stopPoint[2 * k] = pointIndex[packages[k].Pickup];
            _stopPoint[2 * k + 1] = pointIndex[packages[k].Delivery];
        }

        _distance = new double[points.Count, points.Count];
        foreach (var from in points)
        {
            var paths = ShortestPaths<string>.Dijkstra(city, from);
            _paths[from] = paths;
            foreach (var to in points)
            {
                _distance[pointIndex[from], pointIndex[to]] = paths.DistanceTo(to);
            }
        }

        ValidateReachability();
    }

    public int Seed { get; init; } = 2026;

    public int Restarts { get; init; } = 60;

    public int PerturbationsPerRestart { get; init; } = 150;

    public double AcceptanceTolerance { get; init; } = 0.02;

    public List<Stop> Plan()
    {
        var random = new Random(Seed);
        List<int>? best = null;
        var bestCost = double.PositiveInfinity;

        for (var restart = 0; restart < Restarts; restart++)
        {
            var order = Enumerable.Range(0, _packages.Count).ToArray();
            if (restart > 0)
            {
                random.Shuffle(order);
            }

            var route = new List<int>();
            foreach (var k in order)
            {
                InsertPackageAtBestPosition(route, k);
            }

            LocalSearch(route);
            route = IteratedLocalSearch(route, random);

            var cost = Cost(route);
            if (cost < bestCost - Epsilon)
            {
                bestCost = cost;
                best = route;
            }
        }

        return ToStops(best ?? new List<int>());
    }

    public List<Stop> PlanNearestNeighbor()
    {
        var route = new List<int>();
        var pickedUp = new bool[_packages.Count];
        var done = new bool[_packages.Count * 2];
        var current = _startPoint;

        for (var step = 0; step < done.Length; step++)
        {
            var bestStop = -1;
            var bestDistance = double.PositiveInfinity;
            for (var stop = 0; stop < done.Length; stop++)
            {
                var isDelivery = (stop & 1) == 1;
                if (done[stop] || (isDelivery && !pickedUp[stop >> 1]))
                {
                    continue;
                }

                var d = _distance[current, _stopPoint[stop]];
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestStop = stop;
                }
            }

            done[bestStop] = true;
            if ((bestStop & 1) == 0)
            {
                pickedUp[bestStop >> 1] = true;
            }

            route.Add(bestStop);
            current = _stopPoint[bestStop];
        }

        return ToStops(route);
    }

    public double TotalDistance(List<Stop> route)
    {
        var total = 0.0;
        var current = _start;
        foreach (var stop in route)
        {
            total += Distance(current, stop.Location);
            current = stop.Location;
        }

        return total;
    }

    public bool IsValid(List<Stop> route)
    {
        if (route.Count != _packages.Count * 2)
        {
            return false;
        }

        var byId = _packages.ToDictionary(p => p.Id);
        var pickedUp = new HashSet<int>();
        var delivered = new HashSet<int>();

        foreach (var stop in route)
        {
            if (!byId.TryGetValue(stop.PackageId, out var package))
            {
                return false;
            }

            if (stop.Kind == StopKind.Pickup)
            {
                if (stop.Location != package.Pickup || !pickedUp.Add(stop.PackageId))
                {
                    return false;
                }
            }
            else if (stop.Location != package.Delivery
                     || !pickedUp.Contains(stop.PackageId)
                     || !delivered.Add(stop.PackageId))
            {
                return false;
            }
        }

        return delivered.Count == _packages.Count;
    }

    public List<string> PathBetween(string from, string to) => GetPaths(from).PathTo(to);

    public double Distance(string from, string to) => GetPaths(from).DistanceTo(to);

    private ShortestPaths<string> GetPaths(string from)
    {
        if (!_paths.TryGetValue(from, out var paths))
        {
            paths = ShortestPaths<string>.Dijkstra(_city, from);
            _paths[from] = paths;
        }

        return paths;
    }

    private void ValidateReachability()
    {
        var taskPoints = _packages.SelectMany(p => new[] { p.Pickup, p.Delivery }).Distinct().ToList();

        var errors = new List<string>();
        var fromStart = taskPoints.Where(p => !_paths[_start].CanReach(p)).ToList();
        if (fromStart.Count > 0)
        {
            errors.Add($"Desde la posición inicial {_start} no se puede llegar a: {string.Join(", ", fromStart)}.");
        }

        foreach (var from in taskPoints)
        {
            var unreachable = taskPoints.Where(to => !_paths[from].CanReach(to)).ToList();
            if (unreachable.Count > 0)
            {
                errors.Add($"Desde {from} no se puede seguir hacia: {string.Join(", ", unreachable)}.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InputValidationException("Hay puntos que no se pueden alcanzar respetando el sentido de las calles:", errors);
        }
    }

    private List<Stop> ToStops(List<int> route) =>
        route.Select(code =>
        {
            var package = _packages[code >> 1];
            return (code & 1) == 0
                ? new Stop(package.Pickup, StopKind.Pickup, package.Id)
                : new Stop(package.Delivery, StopKind.Delivery, package.Id);
        }).ToList();

    private double Cost(List<int> route)
    {
        var total = 0.0;
        var current = _startPoint;
        foreach (var stop in route)
        {
            total += _distance[current, _stopPoint[stop]];
            current = _stopPoint[stop];
        }

        return total;
    }

    private int PointBefore(List<int> route, int index) =>
        index == 0 ? _startPoint : _stopPoint[route[index - 1]];

    private double ToStopAt(int point, List<int> route, int index) =>
        index < route.Count ? _distance[point, _stopPoint[route[index]]] : 0;

    private double InsertionDelta(List<int> route, int index, int x, int y)
    {
        var a = PointBefore(route, index);
        return _distance[a, x] + ToStopAt(y, route, index) - ToStopAt(a, route, index);
    }

    private void InsertPackageAtBestPosition(List<int> route, int k)
    {
        int pickup = 2 * k, delivery = 2 * k + 1;
        int p = _stopPoint[pickup], d = _stopPoint[delivery];

        var bestDelta = double.PositiveInfinity;
        int bestI = 0, bestJ = 0;

        var bestLaterDelta = double.PositiveInfinity;
        var bestLaterJ = -1;

        for (var i = route.Count; i >= 0; i--)
        {
            var together = InsertionDelta(route, i, p, d) + _distance[p, d];
            if (together < bestDelta - Epsilon)
            {
                (bestDelta, bestI, bestJ) = (together, i, i);
            }

            if (bestLaterJ >= 0)
            {
                var separate = InsertionDelta(route, i, p, p) + bestLaterDelta;
                if (separate < bestDelta - Epsilon)
                {
                    (bestDelta, bestI, bestJ) = (separate, i, bestLaterJ);
                }
            }

            var deliveryDelta = InsertionDelta(route, i, d, d);
            if (deliveryDelta < bestLaterDelta - Epsilon)
            {
                (bestLaterDelta, bestLaterJ) = (deliveryDelta, i);
            }
        }

        route.Insert(bestJ, delivery);
        route.Insert(bestI, pickup);
    }

    private void LocalSearch(List<int> route)
    {
        bool improved;
        do
        {
            improved = RelocatePackages(route);
            improved |= MoveBlocks(route);
        }
        while (improved);
    }

    private bool RelocatePackages(List<int> route)
    {
        var improvedAny = false;
        for (var k = 0; k < _packages.Count; k++)
        {
            var before = Cost(route);
            var backup = new List<int>(route);
            route.Remove(2 * k);
            route.Remove(2 * k + 1);
            InsertPackageAtBestPosition(route, k);

            var after = Cost(route);
            if (after < before - Epsilon)
            {
                improvedAny = true;
            }
            else if (after > before + Epsilon)
            {
                route.Clear();
                route.AddRange(backup);
            }
        }

        return improvedAny;
    }

    private bool MoveBlocks(List<int> route)
    {
        var improvedAny = false;
        for (var length = 1; length <= 3; length++)
        {
            for (var start = 0; start + length <= route.Count; start++)
            {
                if (TryMoveBlock(route, start, length))
                {
                    improvedAny = true;
                }
            }
        }

        return improvedAny;
    }

    private bool TryMoveBlock(List<int> route, int start, int length)
    {
        var block = route.GetRange(start, length);
        int first = _stopPoint[block[0]], last = _stopPoint[block[^1]];

        var a = PointBefore(route, start);
        var removalGain = _distance[a, first] + ToStopAt(last, route, start + length) - ToStopAt(a, route, start + length);

        var rest = new List<int>(route);
        rest.RemoveRange(start, length);

        var low = 0;
        var high = rest.Count;
        foreach (var stop in block)
        {
            var partner = stop ^ 1;
            if (block.Contains(partner))
            {
                continue;
            }

            var partnerIndex = rest.IndexOf(partner);
            if ((stop & 1) == 1)
            {
                low = Math.Max(low, partnerIndex + 1);
            }
            else
            {
                high = Math.Min(high, partnerIndex);
            }
        }

        var bestDelta = -Epsilon;
        var bestIndex = -1;
        for (var index = low; index <= high; index++)
        {
            if (index == start)
            {
                continue;
            }

            var delta = InsertionDelta(rest, index, first, last) - removalGain;
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = index;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        rest.InsertRange(bestIndex, block);
        route.Clear();
        route.AddRange(rest);
        return true;
    }

    private List<int> IteratedLocalSearch(List<int> route, Random random)
    {
        var current = new List<int>(route);
        var currentCost = Cost(current);
        var best = new List<int>(current);
        var bestCost = currentCost;

        for (var round = 0; round < PerturbationsPerRestart; round++)
        {
            var candidate = new List<int>(current);
            var removeCount = Math.Min(_packages.Count, random.Next(2, 7));
            var removed = random.Next(2) == 0
                ? PickRandomPackages(random, removeCount)
                : PickRelatedPackages(random, removeCount);

            foreach (var k in removed)
            {
                candidate.Remove(2 * k);
                candidate.Remove(2 * k + 1);
            }

            foreach (var k in removed)
            {
                InsertPackageAtBestPosition(candidate, k);
            }

            LocalSearch(candidate);
            var cost = Cost(candidate);

            if (cost <= currentCost + Epsilon || cost <= bestCost * (1 + AcceptanceTolerance))
            {
                current = candidate;
                currentCost = cost;
                if (cost < bestCost - Epsilon)
                {
                    best = new List<int>(candidate);
                    bestCost = cost;
                }
            }
        }

        return best;
    }

    private int[] PickRandomPackages(Random random, int count)
    {
        var all = Enumerable.Range(0, _packages.Count).ToArray();
        random.Shuffle(all);
        return all.Take(count).ToArray();
    }

    private int[] PickRelatedPackages(Random random, int count)
    {
        var seed = random.Next(_packages.Count);
        int seedPickup = _stopPoint[2 * seed], seedDelivery = _stopPoint[2 * seed + 1];

        double Closeness(int k)
        {
            int p = _stopPoint[2 * k], d = _stopPoint[2 * k + 1];
            return Math.Min(_distance[seedPickup, p], _distance[p, seedPickup])
                 + Math.Min(_distance[seedDelivery, d], _distance[d, seedDelivery]);
        }

        var related = Enumerable.Range(0, _packages.Count).OrderBy(Closeness).Take(count).ToArray();
        random.Shuffle(related);
        return related;
    }
}
