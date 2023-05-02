﻿using System.Diagnostics;
using System.IO;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Codecs;
using NAudio.Wave;
using NetCoreServer;
using static System.Net.Mime.MediaTypeNames;

namespace AudioSocket.Net.Helper
{
    public class TTSHelper
    {
        private readonly IConfiguration configuration;
        private SpeechConfig speechConfig;
        private PushAudioInputStream audioInputStream;
        private AudioConfig audioConfig;
        private SpeechRecognizer speechRecognizer;
        private TaskCompletionSource<int> stopRecognition;
        private AudioDataStream AudioDataStream;
        private PullAudioOutputStream pullAudioOutputStream;
        private string Ssml;
        private byte[]? Uuid = null;

        public TTSHelper(TcpSession session, string ssml)
        {
            var text = "Ciao a tutti! Sono una intelligenza artificiale e sarò felice di scrivere un lungo testo in italiano per voi. L'Italia è un paese meraviglioso con una cultura e una storia incredibili. Il cibo italiano è famoso in tutto il mondo, e ci sono così tanti posti bellissimi da visitare, dalle città d'arte come Firenze e Roma alle coste mozzafiato come la Costiera Amalfitana e la Sardegna.\n\nUno degli aspetti più affascinanti dell'Italia è la sua storia. Il paese ha una lunga tradizione artistica, e in ogni angolo si possono trovare capolavori come quadri, sculture e edifici antichi. L'Italia è stata anche il centro della civiltà romana, e visite ai luoghi storici come il Colosseo e il Foro Romano possono far rivivere questo passato glorioso.";
            if (ssml is null)
                Ssml = $"""<speak version='1.0' xml:lang='it-IT'><voice xml:lang='it-IT' xml:gender='male' name='Giuseppe_5Neural'><lexicon uri='https://cvoiceproduks.blob.core.windows.net/acc-public-files/a5aa83643a5c4d3fb961fb09a6f82993/81583100-5cfd-43f7-8df4-67561d42031a.xml' />{text}</voice></speak>""";
            else
                Ssml = ssml;

            configuration = SettingHelper.GetConfigurations();
            // Creates an instance of a speech config with specified subscription key and service region.
            // Replace with your own subscription key and service region (e.g., "westus").
            speechConfig = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));

            // Set the voice name, refer to https://aka.ms/speech/voices/neural for full list.
            speechConfig.SpeechSynthesisVoiceName = configuration.GetValue<string>("CognitiveServices:SpeechSynthesisVoiceName");
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw8Khz16BitMonoPcm);
            // Raw16Khz16BitMonoPcm -> Slin
            // Raw8Khz16BitMonoPcm -> format G722 
            speechConfig.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");

            // Creates a speech synthesizer using the default speaker as audio output.
            pullAudioOutputStream = AudioOutputStream.CreatePullStream();
                // Creates a speech synthesizer using audio stream output.
            var streamConfig = AudioConfig.FromStreamOutput(pullAudioOutputStream);
            var speechSynthesizer = new SpeechSynthesizer(speechConfig, streamConfig);

            // TODO try f&f
            var res = speechSynthesizer.StartSpeakingSsmlAsync(Ssml).GetAwaiter().GetResult();
            //var res = speechSynthesizer.SpeakSsmlAsync(Ssml).GetAwaiter().GetResult();
            var x = res.AudioData;
            Console.WriteLine("AudioData lenght " + res.AudioData.Length);
        }

        public uint ConvertTextToSpeechAsync(byte[] buffer)
        {
            var result = pullAudioOutputStream.Read(buffer);

            var shouldDecodeG722 = configuration.GetValue<bool>("CognitiveServices:EncodeWavetoG722");
            if (shouldDecodeG722 is true)
                buffer = EncodeWavetoG722(buffer, 0, buffer.Length);

            return result;
        }

        private byte[] EncodeWavetoG722(byte[] data, int offset, int length)
        {
            G722CodecState _state = new G722CodecState(64000, G722Flags.None);
            G722Codec _codec = new G722Codec();
            if (offset != 0)
            {
                throw new ArgumentException("G722 does not yet support non-zero offsets");
            }

            var wb = new WaveBuffer(data);
            int encodedLength = length / 4; // or 2?

            var outputBuffer = new byte[encodedLength];

            int encoded = _codec.Encode(_state, outputBuffer, wb.ShortBuffer, encodedLength);
            data = outputBuffer;
            return outputBuffer;
        }

    }
}

