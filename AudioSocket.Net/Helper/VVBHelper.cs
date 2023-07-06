using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Enyim.Caching.Memcached;

namespace AudioSocket.Net.Helper
{
    public class VVBHelper
    {
        private readonly MemcachedHelper cacheHelper;

        public VVBHelper(MemcachedHelper cacheHelper)
        {
            this.cacheHelper = cacheHelper;
        }

        public async Task<bool> SetUserMessageAsync(string uuid, string message)
        {
            return await cacheHelper.SetAsync($"usrmsg_{uuid}", message);
        }

        public async Task<string> GetUserMessageAsync(string uuid)
        {
            return await cacheHelper.GetAsync<string>($"usrmsg_{uuid}");
        }

        public async Task<bool> SetBotMessageAsync(string uuid, string message)
        {
            return await cacheHelper.SetAsync($"botmsg_{uuid}", message);
        }

        public async Task<string> GetBotMessageAsync(string uuid)
        {
            return await cacheHelper.GetAsync<string>($"botmsg_{uuid}");
        }

        public async Task<bool> SetBotMessageByMessageHashAsync(string message)
        {
            return await cacheHelper.SetAsync($"botmsghash_{GetHashKey(message)}", message);
        }

        public async Task<string> GetBotMessageByMessageHashAsync(string message)
        {
            return await cacheHelper.GetAsync<string>($"botmsghash_{GetHashKey(message)}");
        }

        public async Task<bool> SetBotAudioAsync(string message, byte[] audio)
        {
            return await cacheHelper.SetAsync($"botaudio_{GetHashKey(message)}", audio);
        }

        public async Task<bool> AppendBotAudioAsync(string message, byte[] audio)
        {
            return await cacheHelper.AppendAsync($"botaudio_{GetHashKey(message)}", audio);
        }

        public async Task<byte[]> GetBotAudioAsync(string message)
        {
            return await cacheHelper.GetAsync<byte[]>($"botaudio_{GetHashKey(message)}");
        }

        public async Task<object> GetBotAudioObjectAsync(string  message)
        {
            return await cacheHelper.GetAsync<object>($"botaudio_{GetHashKey(message)}");
        }

        public async Task<bool> SetAudioCounterAsync(string message, int counter)
        {
            return await cacheHelper.SetAsync<int>($"botaudio_{GetHashKey(message)}", counter);
        }

        private string GetHashKey(string message) {
            using SHA256 mySHA256 = SHA256.Create();
            byte[] hashValue = mySHA256.ComputeHash(Encoding.ASCII.GetBytes(message));
            return BitConverter.ToString(hashValue);
        }
    }
}
