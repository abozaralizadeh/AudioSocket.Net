using System;
using Enyim.Caching.Memcached;

namespace AudioSocket.Net.Helper
{
    public class VvbHelper
    {
        private readonly MemcachedHelper cacheHelper;

        public VvbHelper(MemcachedHelper cacheHelper)
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

        public async Task<bool> SetBotAudioAsync(string uuid, byte[] audio)
        {
            //var hash = hash(message)
            return await cacheHelper.SetAsync($"botaudio_{uuid}", audio);
        }

        public async Task<bool> AppendBotAudioAsync(string uuid, byte[] audio)
        {
            return await cacheHelper.AppendAsync($"botaudio_{uuid}", audio);
        }

        public async Task<byte[]> GetBotAudioAsync(string uuid)
        {
            return await cacheHelper.GetAsync<byte[]>($"botaudio_{uuid}");
        }

        public async Task<object> GetBotAudioObjectAsync(string uuid)
        {
            return await cacheHelper.GetAsync<object>($"botaudio_{uuid}");
        }

        public async Task<bool> SetAudioCounterAsync(string uuid, int counter)
        {
            return await cacheHelper.SetAsync<int>($"botaudio_{uuid}", counter);
        }
    }
}
