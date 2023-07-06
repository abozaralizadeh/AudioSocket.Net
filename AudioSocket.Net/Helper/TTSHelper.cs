using System;
using System.Diagnostics;
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
        private readonly VVBHelper vvbHelper;

        private PullAudioOutputStream pullAudioOutputStream;
        private int? cacheCounter;
        private byte[]? cacheAudio;
        private bool stopCacheWriting;
        private string cacheMessage;
        private bool enableAppend;
        private MemoryStream? cacheAudioStream;

        public TTSHelper(AudioSocketSessionTTS session, VVBHelper vvbHelper, string? ssml = null)
        {
            enableAppend = false;
            stopCacheWriting = false;
            cacheCounter = null;
            cacheAudioStream = null;
            cacheMessage = string.Empty;
            cacheAudio = null;

            var uuid = session.UuidString;

            //TODO: to remove
            var text = "Ciao sono TOBi. Come posso esserti utile?";
            if (ssml is null)
                ssml = $"""<speak version='1.0' xml:lang='it-IT'><voice xml:lang='it-IT' xml:gender='male' name='Giuseppe_5Neural'><lexicon uri='https://cvoiceproduks.blob.core.windows.net/acc-public-files/a5aa83643a5c4d3fb961fb09a6f82993/81583100-5cfd-43f7-8df4-67561d42031a.xml' />{text}</voice></speak>""";

            vvbHelper.SetBotMessageAsync(uuid, ssml).GetAwaiter().GetResult(); //TODO: to remove!

            cacheMessage = vvbHelper.GetBotMessageAsync(uuid).GetAwaiter().GetResult();

            if (!string.IsNullOrEmpty(cacheMessage))
            {
               var audioObj = vvbHelper.GetBotAudioObjectAsync(cacheMessage).GetAwaiter().GetResult();

                if (audioObj != null)
                {
                    if (audioObj is int)
                        cacheCounter = (int)audioObj;
                    else if (audioObj is byte[])
                    {
                        cacheAudio = (byte[])audioObj;
                        cacheAudioStream = new MemoryStream(cacheAudio);
                    }
                }
            }

            //TODO: what we have to do if cacheMessage is empty? throw errors?

            if(cacheAudio == null && !string.IsNullOrEmpty(cacheMessage))
            {
                vvbHelper.SetBotMessageByMessageHashAsync(cacheMessage).GetAwaiter().GetResult();

                //retrieve audio by memcached and check if is counter
                //if audio is null, set on memcache withcounter

                configuration = SettingHelper.GetConfigurations();
                // Creates an instance of a speech config with specified subscription key and service region.
                // Replace with your own subscription key and service region (e.g., "westus").
                var speechConfig = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));

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
                var res = speechSynthesizer.StartSpeakingSsmlAsync(ssml).GetAwaiter().GetResult();
                //var res = speechSynthesizer.SpeakSsmlAsync(Ssml).GetAwaiter().GetResult();
                var x = res.AudioData;
                Console.WriteLine("AudioData lenght " + res.AudioData.Length);
            }
        }

        public uint ConvertTextToSpeechAsync(byte[] buffer)
        {
            if (cacheAudio != null && cacheAudioStream != null)
            {
                //read cacheAudio
                return (uint)cacheAudioStream.Read(buffer);
            }

            var result = pullAudioOutputStream.Read(buffer);

            var shouldDecodeG722 = configuration.GetValue<bool>("CognitiveServices:EncodeWavetoG722");
            if (shouldDecodeG722 is true)
                buffer = G722Helper.EncodeWavetoG722(buffer, 0, buffer.Length);

            var maxCacheCounter = configuration.GetValue<int>("MemCache:CounterThreshold", 3);

            if(stopCacheWriting)
                return result;

            //TODO: clean and move to another method
            if (cacheCounter != null)
            {
                if (cacheCounter.Value == maxCacheCounter)
                {
                    if (enableAppend)
                    {
                        vvbHelper.AppendBotAudioAsync(cacheMessage, buffer).GetAwaiter().GetResult();
                    }
                    else
                    {
                        vvbHelper.SetBotAudioAsync(cacheMessage, buffer).GetAwaiter().GetResult();
                        enableAppend = true;
                    }
                }
                else
                {
                    vvbHelper.SetAudioCounterAsync(cacheMessage, cacheCounter.Value + 1).GetAwaiter().GetResult();
                    stopCacheWriting = true;
                }
            }
            else if(cacheCounter == null && cacheAudio == null)
            {
                vvbHelper.SetAudioCounterAsync(cacheMessage, 1).GetAwaiter().GetResult();
                stopCacheWriting = true;
            }

            return result;
        }
    }
}

