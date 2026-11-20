namespace DeliveryRoutes.Backend;

public static class PackageFile
{
    public static List<Package> Load(string path, Graph<string> city)
    {
        var errors = new List<string>();
        var pickups = new Dictionary<int, (string Location, int Line)>();
        var deliveries = new Dictionary<int, (string Location, int Line)>();

        foreach (var (lineNumber, fields) in FlatFile.Read(path))
        {
            var prefix = $"Línea {lineNumber}:";
            if (fields.Length != 3)
            {
                errors.Add($"{prefix} se esperaban 3 campos (ubicación,acción,paquete) y hay {fields.Length}.");
                continue;
            }

            var (location, action, idText) = (fields[0], fields[1], fields[2]);
            var lineIsValid = true;

            if (!city.ContainsVertex(location))
            {
                errors.Add($"{prefix} la ubicación '{location}' no existe en el mapa.");
                lineIsValid = false;
            }

            Dictionary<int, (string Location, int Line)>? target = null;
            if (action.Equals(nameof(StopKind.Pickup), StringComparison.OrdinalIgnoreCase))
            {
                target = pickups;
            }
            else if (action.Equals(nameof(StopKind.Delivery), StringComparison.OrdinalIgnoreCase))
            {
                target = deliveries;
            }
            else
            {
                errors.Add($"{prefix} la acción '{action}' no es Pickup ni Delivery.");
                lineIsValid = false;
            }

            if (!int.TryParse(idText, out var id) || id <= 0)
            {
                errors.Add($"{prefix} el número de paquete '{idText}' no es un entero mayor que 0.");
                lineIsValid = false;
            }

            if (!lineIsValid || target is null)
            {
                continue;
            }

            if (target.TryGetValue(id, out var previous))
            {
                var what = target == pickups ? "recogida" : "entrega";
                errors.Add($"{prefix} el paquete {id} tiene una segunda {what} (la primera está en la línea {previous.Line}).");
                continue;
            }

            target[id] = (location, lineNumber);
        }

        foreach (var (id, pickup) in pickups.Where(p => !deliveries.ContainsKey(p.Key)))
        {
            errors.Add($"El paquete {id} se recoge (línea {pickup.Line}) pero le falta la entrega.");
        }

        foreach (var (id, delivery) in deliveries.Where(d => !pickups.ContainsKey(d.Key)))
        {
            errors.Add($"El paquete {id} se entrega (línea {delivery.Line}) pero le falta la recogida.");
        }

        if (errors.Count == 0 && pickups.Count == 0)
        {
            errors.Add("El archivo no tiene ningún paquete.");
        }

        if (errors.Count > 0)
        {
            throw new InputValidationException($"Errores en los paquetes '{path}':", errors);
        }

        return pickups.Keys
            .Order()
            .Select(id => new Package(id, pickups[id].Location, deliveries[id].Location))
            .ToList();
    }
}
