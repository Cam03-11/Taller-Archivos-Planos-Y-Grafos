namespace DeliveryRoutes.Backend;

public readonly record struct FlatFileRecord(int LineNumber, string[] Fields);

public static class FlatFile
{
    public static List<FlatFileRecord> Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new InputValidationException($"No se pudo leer el archivo '{path}':", "El archivo no existe.");
        }

        var records = new List<FlatFileRecord>();
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split(',').Select(field => field.Trim()).ToArray();
            records.Add(new FlatFileRecord(lineNumber, fields));
        }

        return records;
    }
}
