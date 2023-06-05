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
        private string Uuid;

        public STTHelper(AudioSocketSessionSTT session)
        {
            configuration = SettingHelper.GetConfigurations();
            speechConfig = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));
            speechConfig.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");
            speechConfig.SpeechRecognitionLanguage = configuration.GetValue<string>("CognitiveServices:SpeechRecognitionLanguage");
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1); // Perfect for G722

            audioInputStream = AudioInputStream.CreatePushStream(audioFormat);
            audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            stopRecognition = new TaskCompletionSource<int>();

            speechRecognizer.Recognizing += (s, e) =>
            {
                Console.WriteLine($"{Uuid} RECOGNIZING: Text={e.Result.Text}");
            };

            speechRecognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"{Uuid} RECOGNIZED: Text={e.Result.Text}");

                    if(!string.IsNullOrEmpty(Uuid) && !string.IsNullOrEmpty(e.Result.Text))
                    {
                        var vvbHelper = new VvbHelper(new MemcachedHelper());
                        vvbHelper.SetUserMessageAsync(Uuid, e.Result.Text).GetAwaiter().GetResult();
                    }

                    // TODO send the right hangup message
                    //var echoBytes = new byte[] { 0x01, 0x10 };
                    //if (Uuid is not null)
                    //    echoBytes = echoBytes.Concat(Uuid).ToArray();
                    //else
                    //    echoBytes = echoBytes.Concat(new byte[] { 0x00 }).ToArray();
                    //session.Send(echoBytes);
                    //echoBytes = new byte[] { 0x00, 0x00, 0x00 };
                    //session.Send(echoBytes);
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"{Uuid} NOMATCH: Speech could not be recognized.");
                }
            };

            speechRecognizer.Canceled += (s, e) =>
            {
                Console.WriteLine($"{Uuid} CANCELED: Reason={e.Reason}");

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
                Console.WriteLine($"\n{Uuid}  Session stopped event.");
                stopRecognition.TrySetResult(0);
            };

            speechRecognizer.StartContinuousRecognitionAsync();
        }

        public void FromStream(byte[] readBytes, string uuid)
        {
            //check if cache contains audio

            var shouldDecodeG722 = configuration.GetValue<bool>("CognitiveServices:DecodeG722toWave");
            if (shouldDecodeG722 is true)
                readBytes = G722Helper.DecodeG722toWave(readBytes, 0, readBytes.Length);

            if (readBytes.Length > 0)
                this.audioInputStream.Write(readBytes, readBytes.Length);
        }
    }
}

