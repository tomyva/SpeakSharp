# SpeakSharp

SpeakSharp is a .NET MAUI speech-coaching app for short, repeatable speaking practice.

## What works

- Random speaking prompts and configurable 15–60 second rounds
- Cross-platform microphone recording and playback
- Automatic stop at the selected duration
- OpenAI transcription that preserves verbal fillers
- Deterministic filler counting and pace metrics
- Structured AI coaching and a clearer rewritten delivery
- AI-generated reference audio with a visible disclosure
- Local practice history and progress summary
- Three configurable daily local reminders on Android and iOS
- A no-key demo/local-analysis path for evaluating the UI immediately

## Run it

Requirements: .NET 10 SDK with the MAUI workload.

```powershell
dotnet restore
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

For Android, select an emulator/device in Visual Studio or use the generated debug APK after building:

```powershell
dotnet build -f net10.0-android
```

The signed debug APK is written to `bin/Debug/net10.0-android/com.speaksharp.app-Signed.apk`.

## AI setup

Open **Settings**, add an OpenAI API key, select a session length, choose reminder times, and save. The prototype stores the key using MAUI Secure Storage. Before distributing this app, replace direct client-side API calls with a small authenticated backend that owns the OpenAI credential and applies per-user rate and spending limits.

The current pipeline uses:

- `gpt-transcribe` for transcription
- `gpt-6-luna` with Structured Outputs for coaching
- `gpt-4o-mini-tts` with the `cedar` voice for reference delivery

Without a key, record a speech, paste/type its transcript, and run the local deterministic analysis—or tap **See sample**.

## Product notes

Mobile platforms generally do not allow third-party apps to force themselves into the foreground. SpeakSharp therefore schedules local coaching notifications; tapping one opens the app. Windows currently saves reminder times as a practice plan but does not schedule OS notifications.
