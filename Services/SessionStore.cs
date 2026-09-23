using System.Text.Json;
using SpeakSharp.Models;

namespace SpeakSharp.Services;

public sealed class SessionStore
{
    private const string FileName = "practice-history.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

    public async Task<IReadOnlyList<PracticeSession>> LoadAsync()
    {
        if (!File.Exists(FilePath)) return [];
        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<List<PracticeSession>>(stream) ?? [];
    }

    public async Task AddAsync(PracticeSession session)
    {
        var sessions = (await LoadAsync()).ToList();
        sessions.Insert(0, session);
        if (sessions.Count > 100) sessions.RemoveRange(100, sessions.Count - 100);
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, sessions, JsonOptions);
    }
}
