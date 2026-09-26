using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using SpeakSharp.Models;

namespace SpeakSharp.Services;

public sealed class OpenAiSpeechCoach(HttpClient httpClient)
{
    private const string KeyName = "openai_api_key";

    public async Task<bool> IsConfiguredAsync() => !string.IsNullOrWhiteSpace(await GetApiKeyAsync());
    public async Task<string> GetApiKeyAsync() => (await SecureStorage.Default.GetAsync(KeyName) ?? string.Empty).Trim();
    public Task SetApiKeyAsync(string value) => string.IsNullOrWhiteSpace(value)
        ? RemoveApiKeyAsync()
        : SecureStorage.Default.SetAsync(KeyName, value.Trim());

    public async Task<string> TranscribeAsync(byte[] wavAudio, CancellationToken cancellationToken = default)
    {
        var apiKey = await RequireApiKeyAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions");
        Authorize(request, apiKey);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("gpt-transcribe"), "model");
        form.Add(new StringContent("Transcribe natural speech exactly. Preserve filler words such as um, uh, er, ah, like, and you know."), "prompt");
        var audio = new ByteArrayContent(wavAudio);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "practice.wav");
        request.Content = form;

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return json.RootElement.GetProperty("text").GetString()?.Trim() ?? string.Empty;
    }

    public async Task<CoachingFeedback> CoachAsync(string topic, string transcript, SpeechAnalysis analysis, CancellationToken cancellationToken = default)
    {
        var apiKey = await RequireApiKeyAsync();
        var schema = new
        {
            type = "object",
            properties = new
            {
                overallScore = new { type = "integer" },
                headline = new { type = "string" },
                strength = new { type = "string" },
                improvement = new { type = "string" },
                nextDrill = new { type = "string" },
                improvedScript = new { type = "string" },
            },
            required = new[] { "overallScore", "headline", "strength", "improvement", "nextDrill", "improvedScript" },
            additionalProperties = false,
        };

        var payload = new
        {
            model = "gpt-6-luna",
            instructions = "You are SpeakSharp, a precise and encouraging public-speaking coach. Evaluate only the supplied transcript and metrics. Never invent audio qualities you cannot observe. Keep every feedback field concise. Rewrite the speech to retain the speaker's meaning while improving clarity, structure, confidence, and removing fillers.",
            input = $"Topic: {topic}\nDuration-derived pace: {analysis.WordsPerMinute:0} words per minute\nDetected fillers: {analysis.FillerCount} ({analysis.FillerRate:0.0}% of words)\nTranscript:\n{transcript}",
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "speech_coaching_feedback",
                    strict = true,
                    schema,
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        Authorize(request, apiKey);
        request.Content = JsonContent.Create(payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var outputText = json.RootElement.GetProperty("output").EnumerateArray()
            .SelectMany(item => item.TryGetProperty("content", out var content) ? content.EnumerateArray() : [])
            .FirstOrDefault(item => item.TryGetProperty("type", out var type) && type.GetString() == "output_text")
            .GetProperty("text").GetString();
        return JsonSerializer.Deserialize<CoachingFeedback>(outputText ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The coach returned an empty response.");
    }

    public async Task<string> GenerateReferenceAudioAsync(string script, CancellationToken cancellationToken = default)
    {
        var apiKey = await RequireApiKeyAsync();
        var payload = new
        {
            model = "gpt-4o-mini-tts",
            voice = "cedar",
            input = script,
            instructions = "Speak like a confident, warm professional. Use purposeful pauses, clear emphasis, natural phrasing, and a conversational pace. Do not sound theatrical.",
            response_format = "mp3",
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "audio/speech");
        Authorize(request, apiKey);
        request.Content = JsonContent.Create(payload);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var path = Path.Combine(FileSystem.CacheDirectory, $"reference-{Guid.NewGuid():N}.mp3");
        await using var output = File.Create(path);
        await response.Content.CopyToAsync(output, cancellationToken);
        return path;
    }

    private static void Authorize(HttpRequestMessage request, string apiKey) => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    private async Task<string> RequireApiKeyAsync()
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Add your OpenAI API key in Settings to enable transcription and AI coaching.");
        return apiKey;
    }

    private static Task RemoveApiKeyAsync()
    {
        SecureStorage.Default.Remove(KeyName);
        return Task.CompletedTask;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = $"OpenAI request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var errorMessage)
                && !string.IsNullOrWhiteSpace(errorMessage.GetString()))
            {
                message = $"OpenAI request failed ({(int)response.StatusCode}): {errorMessage.GetString()}";
            }
        }
        catch (JsonException)
        {
            // Keep the status-based message when a proxy or network appliance
            // returns a non-JSON response.
        }

        Debug.WriteLine(message);
        throw new InvalidOperationException(message);
    }
}
