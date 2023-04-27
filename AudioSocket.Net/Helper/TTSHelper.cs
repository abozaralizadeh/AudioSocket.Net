using System.Diagnostics;
using System.IO;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
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
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm); // Riff24Khz16BitMonoPcm
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
            return pullAudioOutputStream.Read(buffer);
        }

    }
}

