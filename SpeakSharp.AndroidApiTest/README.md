# SpeakSharp Android API Test

This native Android test app isolates the failing path from MAUI, XAML, secure storage, navigation, and notifications.

It tests, on the Android device itself:

1. microphone permission and AAC/M4A recording;
2. HTTPS access to `https://api.openai.com`;
3. API-key authentication; and
4. transcription with `gpt-transcribe`.

The API key is held only in memory. The app does not save it, log it, or include it in the project.

## Run

1. Open `SpeakSharp.AndroidApiTest.slnx` in Visual Studio.
2. Select a physical Android device and run the `SpeakSharp.AndroidApiTest` project.
3. Enter the API key, record at least three seconds, stop, and tap **Send to OpenAI**.
4. Copy the complete PASS or FAIL message from the result area.

The app uses the package ID `com.speaksharp.apitest`, so it can be installed beside the main SpeakSharp app.
