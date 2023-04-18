using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NetCoreServer;
using SIPSorceryMedia.Abstractions;
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
        private string Ssml;
        private byte[]? Uuid = null;

        public TTSHelper(TcpSession session, string ssml)
        {
            var text = "Ciao Sono TOBI! come posso aiutarti?";
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
            speechConfig.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");

            // Creates a speech synthesizer using the default speaker as audio output.
            using (var speechSynthesizer = new SpeechSynthesizer(speechConfig, null))
            {
                var result = speechSynthesizer.SpeakSsmlAsync(Ssml).GetAwaiter().GetResult();
                AudioDataStream = AudioDataStream.FromResult(result);
            }

        }

        public uint ConvertTextToSpeechAsync(byte[] buffer)
        {
            return AudioDataStream.ReadData(buffer);
        }
    }
}

