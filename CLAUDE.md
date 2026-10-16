# CLAUDE.md

Guidance for Claude Code (and humans) working in this repository.

## What this is

AudioSocket.Net is a .NET 7 console server for the Asterisk AudioSocket protocol (described in
README.md) that connects phone calls to Azure AI Speech. One process runs in one of two modes, set by
`AudioSocket:ServerType`:

- **STT**: takes the caller's audio from Asterisk, streams it into an Azure speech recognizer,
  writes each recognised phrase to Memcached under the call's UUID and sends a hangup frame.
- **TTS**: on the call's UUID frame, synthesises speech (or replays cached audio), streams it back
  in 320-byte `0x10` frames about every 20 ms, then sends a hangup frame.

Memcached is the mailbox between this server and whatever drives the conversation; that component
is not in this repo. Libraries: NetCoreServer (TCP), Microsoft.CognitiveServices.Speech, NAudio
(G.722 codec), Enyim.Memcached2 (a preview package).

## Commands

```bash
dotnet build AudioSocket.Net.sln
cd AudioSocket.Net && dotnet run     # from the project folder: appsettings.json is read from the CWD
docker build -f AudioSocket.Net/Dockerfile -t audiosocket-net .   # context must be the repo root
docker run -p 5044:5044 -e AudioSocket__ServerType=STT -e Memcached__Endpoints \
  -e CognitiveServices__SubscriptionKey audiosocket-net           # -e NAME passes the host's value
```

It targets `net7.0` (out of support since May 2024; the Dockerfile uses the 7.0 images). A
Memcached server must be reachable at `Memcached:Endpoints`. There are no tests and no CI.

## Layout

```
AudioSocket.Net/
  Program.cs                 reads AudioSocket:*, starts the STT or TTS server, restarts it if stopped
  AudioSocketBaseSession.cs  frame parser (1-byte type, 2-byte big-endian length), abstract callbacks
  AudioSocketServerSTT.cs    STT server + session: slin payloads -> STTHelper
  AudioSocketServerTTS.cs    TTS server + session: on the UUID frame, TTSHelper -> paced 0x10 frames
  Helper/SettingHelper.cs    builds IConfiguration (appsettings.json, environment, user secrets)
  Helper/STTHelper.cs        SpeechRecognizer over a push stream, optional G.722 decode
  Helper/TTSHelper.cs        SpeechSynthesizer to a pull stream, Memcached audio cache, demo text
  Helper/BridgeHelper.cs     Memcached key scheme; MemcachedHelper.cs wraps Enyim.Memcached2
  Helper/G722Helper.cs       NAudio G.722 encode/decode at 64 kbit/s
  appsettings.json           every setting (placeholders for the Speech key and endpoint id)
  Dockerfile                 multi-stage build
```

## Configuration

`SettingHelper.GetConfigurations()` layers `appsettings.json` (required, from the current working
directory), then environment variables (`Section__Key`), then user secrets (`UserSecretsId` in the
csproj; loaded in every environment, and last, so they win). Keep the Speech key in user secrets
(`dotnet user-secrets set "CognitiveServices:SubscriptionKey" "<key>"` in `AudioSocket.Net/`) or an
environment variable, never in `appsettings.json`.

- `AudioSocket:ServerType` (`STT` or `TTS`; anything else, or missing, throws at startup),
  `AudioSocket:IpAddress` (default `0.0.0.0`), `AudioSocket:Port` (5044 in appsettings.json).
- `Memcached:Endpoints`: `host:port` (the comment in appsettings.json shows several joined by `;`).
- `CognitiveServices:SubscriptionKey`, `Region`, `EndpointId`, `SpeechRecognitionLanguage`,
  `SpeechSynthesisVoiceName`.
- STT: `CognitiveServices:DecodeG722toWave` decodes each payload from G.722 first;
  `InputAudioSamplePerSecond` / `InputAudioBitPerSample` / `InputAudioChannels` (defaults
  16000/16/1) set the recognizer's input format and must match the bytes it is given.
- TTS: `CognitiveServices:EncodeWavetoG722`, and `MemCache:CounterThreshold` (default 3; absent
  from appsettings.json; note `MemCache`, not `Memcached`). `SpeechSynthesisOutputFormat` is never
  read: TTSHelper hard-codes `Raw8Khz16BitMonoPcm`.

## Memcached keys (BridgeHelper)

- `usrmsg_<uuid>`: phrase recognised by the STT server. `botmsg_<uuid>`: SSML for the TTS server.
- `botaudio_<hash>` holds either a counter or the audio bytes for a message (`botmsghash_<hash>`
  holds the message). Each synthesis bumps the counter; the first synthesis after it reaches
  `MemCache:CounterThreshold` writes the audio, and later sessions replay it from Memcached.
- `<uuid>` and `<hash>` are `BitConverter.ToString` output: uppercase hex pairs joined by `-`
  (`40-32-5E-C2-...`), not the canonical GUID string. Anything else using these keys must match.

## Conventions / gotchas

- TTSHelper overwrites `botmsg_<uuid>` with a hard-coded Italian demo text (marked
  `TODO: to remove!`), and synthesis speaks its local `ssml` variable, not the value read back from
  Memcached. So the TTS server always speaks the demo text; removing the overwrite also means
  synthesising the Memcached value.
- In `ConvertTextToSpeechAsync` the G.722 result is assigned to the local `buffer` parameter, so it
  only reaches the cache: Asterisk gets the raw PCM in `Sentbuffer`, a cache hit the encoded bytes.
- In `ProcessBuffer`, a split header (`LastBytes`) is prepended to the next read, but the loop bound
  and payload maths still use the original `bufferSize`.
- The csproj uses the Web SDK (configuration packages, implicit usings) but there is no HTTP host:
  `applicationUrl` in launchSettings.json, `EXPOSE 80` in the Dockerfile and
  `appsettings.Development.json` have no effect. The server listens on TCP `AudioSocket:Port`.
- `GetConfigurations()` runs per session and builds a new configuration (with a file watcher) each
  time. The TTS send loop runs synchronously inside the receive callback until the audio ends.
- Base-session log lines say `AudioSocketTTS` in STT mode too, and every TCP read logs a line.
  `SendKindId()` (which writes a 2-byte header, not 3) and `Worker.RequestStop()` are unused.
- In the container, `localhost:11211` is the container itself: set `Memcached__Endpoints`.
- README.md's protocol and Asterisk sections come from the upstream CyCoreSystems/audiosocket
  project (the godoc badge points at its Go library); its dialplan examples use port 9092, while
  this server defaults to 5044.
- Sources are UTF-8 with BOM; `TTSHelper.cs`, the csproj and the Dockerfile use CRLF. Keep both.
