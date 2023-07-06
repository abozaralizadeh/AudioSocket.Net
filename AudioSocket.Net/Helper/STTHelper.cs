using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.VisualBasic;
using NAudio.Codecs;
using NAudio.Wave;
using NetCoreServer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AudioSocket.Net.Helper
{
    public class STTHelper
    {
        private readonly IConfiguration configuration;
        private SpeechConfig speechConfig;
        private PushAudioInputStream audioInputStream;
        private AudioConfig audioConfig;
        private SpeechRecognizer speechRecognizer;
        private TaskCompletionSource<int> stopRecognition;

        public STTHelper(AudioSocketSessionSTT session, VVBHelper vvbHelper)
        {
            configuration = SettingHelper.GetConfigurations();
            speechConfig = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));
            speechConfig.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");
            speechConfig.SpeechRecognitionLanguage = configuration.GetValue<string>("CognitiveServices:SpeechRecognitionLanguage");
            var InputAudioSamplePerSecond = configuration.GetValue<uint>("CognitiveServices:InputAudioSamplePerSecond", 16000);
            var InputAudioBitPerSample = configuration.GetValue<byte>("CognitiveServices:InputAudioBitPerSample", 16);
            var InputAudioChannels = configuration.GetValue<byte>("CognitiveServices:InputAudioChannels", 1);
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(InputAudioSamplePerSecond, InputAudioBitPerSample, InputAudioChannels); // Default is Perfect for G722

            audioInputStream = AudioInputStream.CreatePushStream(audioFormat);
            audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            stopRecognition = new TaskCompletionSource<int>();

            speechRecognizer.Recognizing += (s, e) =>
            {
                Console.WriteLine($"{session.UuidString} RECOGNIZING: Text={e.Result.Text}");
            };

            speechRecognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"{session.UuidString} RECOGNIZED: Text={e.Result.Text}");

                    if(!string.IsNullOrEmpty(session.UuidString) && !string.IsNullOrEmpty(e.Result.Text))
                    {
                        vvbHelper.SetUserMessageAsync(session.UuidString, e.Result.Text).GetAwaiter().GetResult();
                    }

                    // TODO send the right hangup message
                    session.SendHangupMessage();
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"{session.UuidString} NOMATCH: Speech could not be recognized.");
                }
            };

            speechRecognizer.Canceled += (s, e) =>
            {
                Console.WriteLine($"{session.UuidString} CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                    Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                }

                stopRecognition.TrySetResult(0);
            };

            speechRecognizer.SessionStopped += (s, e) =>
            {
                Console.WriteLine($"\n{session.UuidString}  Session stopped event.");
                stopRecognition.TrySetResult(0);
            };

            speechRecognizer.StartContinuousRecognitionAsync();
        }

        public void FromStream(byte[] readBytes)
        {
            var shouldDecodeG722 = configuration.GetValue<bool>("CognitiveServices:DecodeG722toWave");
            if (shouldDecodeG722 is true)
                readBytes = G722Helper.DecodeG722toWave(readBytes, 0, readBytes.Length);

            if (readBytes.Length > 0)
                this.audioInputStream.Write(readBytes, readBytes.Length);
        }
    }
}

