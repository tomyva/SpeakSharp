using System.Net.Http.Headers;
using System.Text.Json;
using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace SpeakSharp.AndroidApiTest;

[Activity(
    Label = "SpeakSharp API Test",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    Theme = "@android:style/Theme.Material.Light.NoActionBar")]
public sealed class MainActivity : Activity
{
    private const int RecordAudioRequestCode = 1001;
    private const string TranscriptionModel = "gpt-transcribe";

    private EditText? _apiKeyEntry;
    private Button? _recordButton;
    private Button? _sendButton;
    private TextView? _statusText;
    private MediaRecorder? _recorder;
    private string? _recordingPath;
    private bool _isRecording;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Title = "SpeakSharp API Test";
        SetContentView(BuildContent());
    }

    private View BuildContent()
    {
        var padding = Dp(20);
        var stack = new LinearLayout(this)
        {
            Orientation = Android.Widget.Orientation.Vertical
        };
        stack.SetPadding(padding, padding, padding, padding);

        var title = new TextView(this)
        {
            Text = "SpeakSharp Android API Test",
            TextSize = 24,
            Typeface = Typeface.DefaultBold
        };
        title.SetTextColor(Color.Rgb(23, 28, 44));
        stack.AddView(title, MatchWrap());

        var explanation = new TextView(this)
        {
            Text = "This isolated test records a short sample on this phone and sends it directly to OpenAI using gpt-transcribe. The key stays in memory and is never saved.",
            TextSize = 16
        };
        explanation.SetTextColor(Color.Rgb(75, 85, 99));
        explanation.SetPadding(0, Dp(8), 0, Dp(16));
        stack.AddView(explanation, MatchWrap());

        _apiKeyEntry = new EditText(this)
        {
            Hint = "OpenAI API key",
            InputType = InputTypes.ClassText | InputTypes.TextVariationPassword
        };
        _apiKeyEntry.SetSingleLine(true);
        stack.AddView(_apiKeyEntry, MatchWrap());

        _recordButton = new Button(this)
        {
            Text = "Record sample"
        };
        _recordButton.Click += (_, _) => ToggleRecording();
        stack.AddView(_recordButton, MatchWrap(Dp(12)));

        _sendButton = new Button(this)
        {
            Text = "Send to OpenAI",
            Enabled = false
        };
        _sendButton.Click += async (_, _) => await SendRecordingAsync();
        stack.AddView(_sendButton, MatchWrap(Dp(8)));

        _statusText = new TextView(this)
        {
            Text = "Ready. Record at least three seconds of speech.",
            TextSize = 16
        };
        _statusText.SetTextIsSelectable(true);
        _statusText.SetTextColor(Color.Rgb(23, 28, 44));
        _statusText.SetPadding(0, Dp(18), 0, Dp(20));
        stack.AddView(_statusText, MatchWrap());

        var scroll = new ScrollView(this);
        scroll.AddView(stack);
        return scroll;
    }

    private void ToggleRecording()
    {
        if (_isRecording)
        {
            StopRecording();
            return;
        }

        if (CheckSelfPermission(Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            RequestPermissions([Android.Manifest.Permission.RecordAudio], RecordAudioRequestCode);
            SetStatus("Microphone permission is required. After allowing it, tap Record sample again.");
            return;
        }

        StartRecording();
    }

    private void StartRecording()
    {
        try
        {
            ReleaseRecorder();
            _recordingPath = System.IO.Path.Combine(CacheDir?.AbsolutePath ?? FilesDir!.AbsolutePath, "speaksharp-api-test.m4a");

#pragma warning disable CA1422
            _recorder = OperatingSystem.IsAndroidVersionAtLeast(31)
                ? new MediaRecorder(this)
                : new MediaRecorder();
#pragma warning restore CA1422

            _recorder.SetAudioSource(AudioSource.Mic);
            _recorder.SetOutputFormat(OutputFormat.Mpeg4);
            _recorder.SetAudioEncoder(AudioEncoder.Aac);
            _recorder.SetAudioSamplingRate(16_000);
            _recorder.SetAudioEncodingBitRate(64_000);
            _recorder.SetOutputFile(_recordingPath);
            _recorder.Prepare();
            _recorder.Start();

            _isRecording = true;
            _recordButton!.Text = "Stop recording";
            _sendButton!.Enabled = false;
            SetStatus("Recording… Speak naturally for at least three seconds, then tap Stop recording.");
        }
        catch (Exception ex)
        {
            ReleaseRecorder();
            _isRecording = false;
            SetStatus($"Could not start recording: {ex.Message}");
        }
    }

    private void StopRecording()
    {
        try
        {
            _recorder?.Stop();
            _isRecording = false;
            _recordButton!.Text = "Record again";
            _sendButton!.Enabled = File.Exists(_recordingPath);
            SetStatus("Sample recorded. Enter the API key and tap Send to OpenAI.");
        }
        catch (Exception ex)
        {
            _isRecording = false;
            _recordButton!.Text = "Record sample";
            _sendButton!.Enabled = false;
            SetStatus($"Recording was too short or could not be saved: {ex.Message}");
        }
        finally
        {
            ReleaseRecorder();
        }
    }

    private async Task SendRecordingAsync()
    {
        var apiKey = _apiKeyEntry?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetStatus("Enter an OpenAI API key first. It will be used for this request only and will not be saved.");
            _apiKeyEntry?.RequestFocus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_recordingPath) || !File.Exists(_recordingPath))
        {
            SetStatus("No recording was found. Record a new sample first.");
            return;
        }

        HideKeyboard();
        SetBusy(true);
        SetStatus($"Sending {new FileInfo(_recordingPath).Length / 1024.0:F1} KB to {TranscriptionModel}…");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(TranscriptionModel), "model");
            form.Add(new StringContent("Transcribe natural speech exactly. Preserve filler words such as um, uh, er, ah, like, and you know."), "prompt");

            await using var fileStream = File.OpenRead(_recordingPath);
            using var audio = new StreamContent(fileStream);
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/mp4");
            form.Add(audio, "file", "practice.m4a");
            request.Content = form;

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"FAIL — HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n{ReadApiError(responseBody)}");
                return;
            }

            using var json = JsonDocument.Parse(responseBody);
            var transcript = json.RootElement.TryGetProperty("text", out var text)
                ? text.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(transcript))
            {
                SetStatus("FAIL — OpenAI returned success but no transcript text.");
                return;
            }

            SetStatus($"PASS — Android reached OpenAI and {TranscriptionModel} accepted the recording.\n\nTranscript:\n{transcript}");
        }
        catch (TaskCanceledException)
        {
            SetStatus("FAIL — The request timed out after 90 seconds.");
        }
        catch (Exception ex)
        {
            SetStatus($"FAIL — {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static string ReadApiError(string responseBody)
    {
        try
        {
            using var json = JsonDocument.Parse(responseBody);
            if (json.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "OpenAI returned an error without a message.";
            }
        }
        catch (JsonException)
        {
            // A concise non-JSON body is still useful for this diagnostic app.
        }

        return string.IsNullOrWhiteSpace(responseBody)
            ? "OpenAI returned an empty error response."
            : responseBody.Length <= 1_000 ? responseBody : responseBody[..1_000];
    }

    private void SetBusy(bool busy)
    {
        if (_sendButton is not null)
        {
            _sendButton.Enabled = !busy && !_isRecording && File.Exists(_recordingPath);
        }

        if (_recordButton is not null)
        {
            _recordButton.Enabled = !busy;
        }

        if (_apiKeyEntry is not null)
        {
            _apiKeyEntry.Enabled = !busy;
        }
    }

    private void SetStatus(string message)
    {
        if (_statusText is not null)
        {
            _statusText.Text = message;
        }
    }

    private void HideKeyboard()
    {
        var input = GetSystemService(InputMethodService) as InputMethodManager;
        input?.HideSoftInputFromWindow(_apiKeyEntry?.WindowToken, HideSoftInputFlags.None);
        _apiKeyEntry?.ClearFocus();
    }

    private void ReleaseRecorder()
    {
        if (_recorder is null)
        {
            return;
        }

        try
        {
            _recorder.Reset();
            _recorder.Release();
        }
        catch
        {
            // The recorder may already have released native resources after an error.
        }
        finally
        {
            _recorder.Dispose();
            _recorder = null;
        }
    }

    protected override void OnDestroy()
    {
        if (_isRecording)
        {
            try
            {
                _recorder?.Stop();
            }
            catch
            {
                // The activity is closing; best-effort cleanup is sufficient.
            }
        }

        ReleaseRecorder();
        base.OnDestroy();
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);

    private static LinearLayout.LayoutParams MatchWrap(int topMargin = 0) => new(
        ViewGroup.LayoutParams.MatchParent,
        ViewGroup.LayoutParams.WrapContent)
    {
        TopMargin = topMargin
    };
}
