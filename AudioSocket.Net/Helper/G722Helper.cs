using System;
using Enyim.Caching.Memcached;
using NAudio.Codecs;
using NAudio.Wave;

namespace AudioSocket.Net.Helper
{
    public static class G722Helper
    {

        public static byte[] EncodeWavetoG722(byte[] data, int offset, int length)
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

        public static byte[] DecodeG722toWave(byte[] data, int offset, int length)
        {
            G722CodecState _state = new G722CodecState(64000, G722Flags.None);
            G722Codec _codec = new G722Codec();
            if (offset != 0)
            {
                throw new ArgumentException("G722 does not yet support non-zero offsets");
            }
            int decodedLength = length * 4;
            var outputBuffer = new byte[decodedLength];
            var wb = new WaveBuffer(outputBuffer);
            int decoded = _codec.Decode(_state, wb.ShortBuffer, data, length);
            return outputBuffer;
        }
    }
}
