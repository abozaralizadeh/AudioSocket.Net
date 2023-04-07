using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace AudioSocket.Net.Helper
{
    public class SpeechHelper
    {
        private readonly IConfiguration configuration;
        private SpeechConfig speechConfig;
        private PushAudioInputStream audioInputStream;
        private AudioConfig audioConfig;
        private SpeechRecognizer speechRecognizer;
        private TaskCompletionSource<int> stopRecognition;

        public SpeechHelper(string id=null)
        {
            if (id is null)
                id = string.Empty;
            configuration = SettingHelper.GetConfigurations();
            speechConfig = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));
            speechConfig.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");
            speechConfig.SpeechRecognitionLanguage = configuration.GetValue<string>("CognitiveServices:SpeechRecognitionLanguage");
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(8000, 16, 1);
            audioInputStream = AudioInputStream.CreatePushStream(audioFormat);
            audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            //speechConfig.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "1000");
            //speechConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
            //speechConfig.SetProperty(PropertyId.SpeechServiceResponse_StablePartialResultThreshold, "2");
            //speechConfig.SetProperty(speechConfig. .PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "10000"); // 10000ms //PropertyId.SpeechServiceResponse_StablePartialResultThreshold
            stopRecognition = new TaskCompletionSource<int>();
            speechRecognizer.Recognizing += (s, e) =>
            {
                Console.WriteLine($"{id} RECOGNIZING: Text={e.Result.Text}");
            };

            speechRecognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"{id} RECOGNIZED: Text={e.Result.Text}");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"{id} NOMATCH: Speech could not be recognized.");
                }
                //speechRecognizer.StartContinuousRecognitionAsync();
            };

            speechRecognizer.Canceled += (s, e) =>
            {
                Console.WriteLine($"{id} CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                    Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                }

                stopRecognition.TrySetResult(0);
                //speechRecognizer.StartContinuousRecognitionAsync();
            };

            speechRecognizer.SessionStopped += (s, e) =>
            {
                Console.WriteLine($"\n{id}  Session stopped event.");
                stopRecognition.TrySetResult(0);
                //speechRecognizer.StartContinuousRecognitionAsync();
            };

            speechRecognizer.StartContinuousRecognitionAsync();
        }

        public async Task<SpeechSynthesisResult> ConvertTextToSpeechAsync(string text)
        {
            // Creates an instance of a speech config with specified subscription key and service region.
            // Replace with your own subscription key and service region (e.g., "westus").
            var config = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));

            // Set the voice name, refer to https://aka.ms/speech/voices/neural for full list.
            config.SpeechSynthesisVoiceName = configuration.GetValue<string>("CognitiveServices:SpeechSynthesisVoiceName");
            config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm);
            config.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");


            // Creates a speech synthesizer using the default speaker as audio output.
            using (var synthesizer = new SpeechSynthesizer(config))
            {
                var ssml = $"""<speak version='1.0' xml:lang='it-IT'><voice xml:lang='it-IT' xml:gender='male' name='Giuseppe_5Neural'><lexicon uri='https://cvoiceproduks.blob.core.windows.net/acc-public-files/a5aa83643a5c4d3fb961fb09a6f82993/81583100-5cfd-43f7-8df4-67561d42031a.xml' />{text}</voice></speak>""";

                return await synthesizer.SpeakSsmlAsync(ssml);
            }
        }

        //This method get the audio from a wav file and re
        public async Task<SpeechRecognitionResult> RecognizeSpeechAsync()
        {
            var config = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));
            config.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");
            config.SpeechRecognitionLanguage = configuration.GetValue<string>("CognitiveServices:SpeechRecognitionLanguage");

            using var audioConfig = AudioConfig.FromWavFileInput(@"test.wav");

            // Creates a speech recognizer.
            using (var recognizer = new SpeechRecognizer(config, audioConfig))
            {
                // Starts speech recognition, and returns after a single utterance is recognized. The end of a
                // single utterance is determined by listening for silence at the end or until a maximum of 15
                // seconds of audio is processed.  The task returns the recognition text as result. 
                // Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
                // shot recognition like command or query. 
                // For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
                var result = await recognizer.RecognizeOnceAsync();

                var text = result.Text;

                return result;
            }
        }

        public void FromStream(byte[] readBytes)
        {
            if (readBytes.Length > 0)
                this.audioInputStream.Write(readBytes, readBytes.Length);
        }

        //Test recognition with audio stream
        public async Task RecognitionWithPushAudioStreamAsync()
        {
            var config = SpeechConfig.FromSubscription(configuration.GetValue<string>("CognitiveServices:SubscriptionKey"), configuration.GetValue<string>("CognitiveServices:Region"));
            config.EndpointId = configuration.GetValue<string>("CognitiveServices:EndpointId");
            config.SpeechRecognitionLanguage = configuration.GetValue<string>("CognitiveServices:SpeechRecognitionLanguage");

            var stopRecognition = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Create a push stream
            using (var pushStream = AudioInputStream.CreatePushStream())
            {
                using (var audioInput = AudioConfig.FromStreamInput(pushStream))
                {
                    // Creates a speech recognizer using audio stream input.
                    using (var recognizer = new SpeechRecognizer(config, audioInput))
                    {
                        // Subscribes to events.
                        //recognizer.Recognizing += (s, e) =>
                        //{
                        //    Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
                        //};

                        //recognizer.Recognized += (s, e) =>
                        //{
                        //    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                        //    {
                        //        Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                        //    }
                        //    else if (e.Result.Reason == ResultReason.NoMatch)
                        //    {
                        //        Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                        //    }
                        //};

                        //recognizer.Canceled += (s, e) =>
                        //{
                        //    Console.WriteLine($"CANCELED: Reason={e.Reason}");

                        //    if (e.Reason == CancellationReason.Error)
                        //    {
                        //        Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                        //        Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                        //        Console.WriteLine($"CANCELED: Did you update the subscription info?");
                        //    }

                        //    stopRecognition.TrySetResult(0);
                        //};

                        //recognizer.SessionStarted += (s, e) =>
                        //{
                        //    Console.WriteLine("\nSession started event.");
                        //};

                        //recognizer.SessionStopped += (s, e) =>
                        //{
                        //    Console.WriteLine("\nSession stopped event.");
                        //    Console.WriteLine("\nStop recognition.");
                        //    stopRecognition.TrySetResult(0);
                        //};

                        // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                        //await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                        // open and read the wave file and push the buffers into the recognizer
                        //using (BinaryAudioStreamReader reader = Helper.CreateWavReader("/Users/simonebitti/Documents/GitHub/FastAGI.net/src/FastAgi/Helpers/test.wav"))
                        //{
                        //    byte[] buffer = new byte[1000];
                        //    while (true)
                        //    {
                        //        var readSamples = reader.Read(buffer, (uint)buffer.Length);
                        //        if (readSamples == 0)
                        //        {
                        //            break;
                        //        }
                        //        pushStream.Write(buffer, readSamples);
                        //    }
                        //}
                        //pushStream.Close();

                        //var bytes = File.ReadAllBytes("/Users/simonebitti/Documents/GitHub/FastAGI.net/src/FastAgi/Helpers/test.wav");
                        ////pushStream.Write(bytes);
                        //var base64 = Convert.ToBase64String(bytes);


                        byte[] forwardsWavFileStreamByteArray;
                        using (FileStream forwardsWavFileStream = new FileStream(@"testMicrosoft.wav", FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            forwardsWavFileStreamByteArray = new byte[forwardsWavFileStream.Length];
                            forwardsWavFileStream.Read(forwardsWavFileStreamByteArray, 0, (int)forwardsWavFileStream.Length);
                        }

                        var base64 = Convert.ToBase64String(forwardsWavFileStreamByteArray);

                        // Waits for completion.
                        //// Use Task.WaitAny to keep the task rooted.
                        //Task.WaitAny(new[] { stopRecognition.Task });
                        pushStream.Write(forwardsWavFileStreamByteArray);
                        var i = await recognizer.RecognizeOnceAsync();

                        var text = i.Text;
                        // Stops recognition.
                        //await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }

    #region MicrosoftTest

    /// <summary>
    /// QUESTI HELPER SONO STATI COPIATI DAL PROGETTO DI MICROSOFT DI GITHUB
    /// </summary>

    public class Helper
    {
        public static AudioConfig OpenWavFile(string filename, AudioProcessingOptions audioProcessingOptions = null)
        {
            BinaryReader reader = new BinaryReader(File.OpenRead(filename));
            return OpenWavFile(reader, audioProcessingOptions);
        }

        public static AudioConfig OpenWavFile(BinaryReader reader, AudioProcessingOptions audioProcessingOptions = null)
        {
            AudioStreamFormat format = readWaveHeader(reader);
            return (audioProcessingOptions == null)
                    ? AudioConfig.FromStreamInput(new BinaryAudioStreamReader(reader), format)
                    : AudioConfig.FromStreamInput(new BinaryAudioStreamReader(reader), format, audioProcessingOptions);
        }

        public static BinaryAudioStreamReader CreateWavReader(string filename)
        {
            BinaryReader reader = new BinaryReader(File.OpenRead(filename));
            // read the wave header so that it won't get into the in the following readings
            AudioStreamFormat format = readWaveHeader(reader);
            return new BinaryAudioStreamReader(reader);
        }

        public static BinaryAudioStreamReader CreateBinaryFileReader(string filename)
        {
            BinaryReader reader = new BinaryReader(File.OpenRead(filename));
            return new BinaryAudioStreamReader(reader);
        }

        public static AudioStreamFormat readWaveHeader(BinaryReader reader)
        {
            // Tag "RIFF"
            char[] data = new char[4];
            reader.Read(data, 0, 4);
            Trace.Assert((data[0] == 'R') && (data[1] == 'I') && (data[2] == 'F') && (data[3] == 'F'), "Wrong wav header");

            // Chunk size
            long fileSize = reader.ReadInt32();

            // Subchunk, Wave Header
            // Subchunk, Format
            // Tag: "WAVE"
            reader.Read(data, 0, 4);
            Trace.Assert((data[0] == 'W') && (data[1] == 'A') && (data[2] == 'V') && (data[3] == 'E'), "Wrong wav tag in wav header");

            // Tag: "fmt"
            reader.Read(data, 0, 4);
            Trace.Assert((data[0] == 'f') && (data[1] == 'm') && (data[2] == 't') && (data[3] == ' '), "Wrong format tag in wav header");

            // chunk format size
            var formatSize = reader.ReadInt32();
            var formatTag = reader.ReadUInt16();
            var channels = reader.ReadUInt16();
            var samplesPerSecond = reader.ReadUInt32();
            var avgBytesPerSec = reader.ReadUInt32();
            var blockAlign = reader.ReadUInt16();
            var bitsPerSample = reader.ReadUInt16();

            // Until now we have read 16 bytes in format, the rest is cbSize and is ignored for now.
            if (formatSize > 16)
                reader.ReadBytes((int)(formatSize - 16));

            // Handle optional LIST chunk.
            // tag: "LIST"
            reader.Read(data, 0, 4);
            if (data[0] == 'L' && data[1] == 'I' && data[2] == 'S' && data[3] == 'T')
            {
                var listChunkSize = reader.ReadUInt32();
                reader.ReadBytes((int)listChunkSize);
                reader.Read(data, 0, 4);
            }

            // Second Chunk, data
            // tag: "data"
            Trace.Assert((data[0] == 'd') && (data[1] == 'a') && (data[2] == 't') && (data[3] == 'a'), "Wrong data tag in wav");
            // data chunk size
            int dataSize = reader.ReadInt32();

            // now, we have the format in the format parameter and the
            // reader set to the start of the body, i.e., the raw sample data
            return AudioStreamFormat.GetWaveFormatPCM(samplesPerSecond, (byte)bitsPerSample, (byte)channels);
        }
    }

    /// <summary>
    /// Adapter class to the native stream api.
    /// </summary>
    public sealed class BinaryAudioStreamReader : PullAudioInputStreamCallback
    {
        private System.IO.BinaryReader _reader;

        /// <summary>
        /// Creates and initializes an instance of BinaryAudioStreamReader.
        /// </summary>
        /// <param name="reader">The underlying stream to read the audio data from. Note: The stream contains the bare sample data, not the container (like wave header data, etc).</param>
        public BinaryAudioStreamReader(System.IO.BinaryReader reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// Creates and initializes an instance of BinaryAudioStreamReader.
        /// </summary>
        /// <param name="stream">The underlying stream to read the audio data from. Note: The stream contains the bare sample data, not the container (like wave header data, etc).</param>
        public BinaryAudioStreamReader(System.IO.Stream stream)
            : this(new System.IO.BinaryReader(stream))
        {
        }

        /// <summary>
        /// Reads binary data from the stream.
        /// </summary>
        /// <param name="dataBuffer">The buffer to fill</param>
        /// <param name="size">The size of data in the buffer.</param>
        /// <returns>The number of bytes filled, or 0 in case the stream hits its end and there is no more data available.
        /// If there is no data immediate available, Read() blocks until the next data becomes available.</returns>
        public override int Read(byte[] dataBuffer, uint size)
        {
            return _reader.Read(dataBuffer, 0, (int)size);
        }

        /// <summary>
        /// This method performs cleanup of resources.
        /// The Boolean parameter <paramref name="disposing"/> indicates whether the method is called from <see cref="IDisposable.Dispose"/> (if <paramref name="disposing"/> is true) or from the finalizer (if <paramref name="disposing"/> is false).
        /// Derived classes should override this method to dispose resource if needed.
        /// </summary>
        /// <param name="disposing">Flag to request disposal.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                _reader.Dispose();
            }

            disposed = true;
            base.Dispose(disposing);
        }


        private bool disposed = false;
    }

    /// <summary>
    /// Implements a custom class for PushAudioOutputStreamCallback.
    /// This is to receive the audio data when the synthesizer has produced audio data.
    /// </summary>
    public sealed class PushAudioOutputStreamSampleCallback : PushAudioOutputStreamCallback
    {
        private byte[] audioData;
        private System.DateTime dt;
        private bool firstWrite = true;
        private double latency = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        public PushAudioOutputStreamSampleCallback()
        {
            Reset();
        }

        /// <summary>
        /// A callback which is invoked when the synthesizer has a output audio chunk to write out
        /// </summary>
        /// <param name="dataBuffer">The output audio chunk sent by synthesizer</param>
        /// <returns>Tell synthesizer how many bytes are received</returns>
        public override uint Write(byte[] dataBuffer)
        {
            if (firstWrite)
            {
                firstWrite = false;
                latency = (DateTime.Now - dt).TotalMilliseconds;
            }

            int oldSize = audioData.Length;
            Array.Resize(ref audioData, oldSize + dataBuffer.Length);
            for (int i = 0; i < dataBuffer.Length; ++i)
            {
                audioData[oldSize + i] = dataBuffer[i];
            }

            Console.WriteLine($"{dataBuffer.Length} bytes received.");

            return (uint)dataBuffer.Length;
        }

        /// <summary>
        /// A callback which is invoked when the synthesizer is about to close the stream
        /// </summary>
        public override void Close()
        {
            Console.WriteLine("Push audio output stream closed.");
        }

        /// <summary>
        /// Get the received audio data
        /// </summary>
        /// <returns>The received audio data in byte array</returns>
        public byte[] GetAudioData()
        {
            return audioData;
        }

        /// <summary>
        /// reset stream
        /// </summary>
        public void Reset()
        {
            audioData = new byte[0];
            dt = DateTime.Now;
            firstWrite = true;
        }


        /// <summary>
        /// get latecny
        /// </summary>
        /// <returns></returns>
        public double GetLatency()
        {
            return latency;
        }
    }


    #endregion
}

