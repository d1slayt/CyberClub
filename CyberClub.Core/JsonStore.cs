using System.Text.Json;

namespace CyberClub.Core;
public sealed class JsonStore(string path)
{
    public ClubState Load()
    {
        if (!File.Exists(path))
            return new ClubState();
        var state = JsonSerializer.Deserialize<ClubState>(File.ReadAllText(path)) ?? throw new InvalidDataException("Файл данных пуст.");
        if (state.Tariffs == null || state.Sessions == null || state.Tariffs.Count != 2 || state.Tariffs.Select(t => t.Name).Distinct().Count() != 2 || state.Tariffs.Any(t => string.IsNullOrWhiteSpace(t.Name) || t.HourlyRate <= 0 || t.HourlyRate > 10000) || state.Sessions.Any(s => s.Station < 1 || s.Station > ClubService.StationCount || s.Rate <= 0 || s.Cost < 0 || s.Finished < s.Started) || state.Sessions.Select(s => s.Id).Distinct().Count() != state.Sessions.Count || state.Sessions.Where(s => s.Finished == null).GroupBy(s => s.Station).Any(g => g.Count() > 1))
            throw new InvalidDataException("Нарушена структура файла данных.");
        return state;
    }

    public void Save(ClubState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path))
            File.Replace(temporary, path, path + ".bak", true);
        else
            File.Move(temporary, path);
    }
}
