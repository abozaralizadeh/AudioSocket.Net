using System;
using Enyim.Caching.Memcached;

namespace AudioSocket.Net.Helper
{
    public class MemcachedHelper: IDisposable
    {
        private readonly MemcachedCluster memcachedCluster;
        private readonly IMemcachedClient memcachedClient;
        private readonly IConfiguration configuration;

        public MemcachedHelper()
        {
            configuration = SettingHelper.GetConfigurations();
            var memCachedEnpoints = configuration.GetValue<string>("MemCached:Endpoints");
            memcachedCluster = new MemcachedCluster("localhost:11211"); //TODO: getvalue from settings
            memcachedCluster.Start();
            memcachedClient = memcachedCluster.GetClient();
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var result = await memcachedClient.GetAsync<T>(key);

            return result;
        }

        public async Task<bool> SetAsync<T>(string key, T value)
        {
            if (value == null)
                return false;

            var result = await memcachedClient.SetAsync(key, value);

            return result;
        }

        public async Task<bool> AppendAsync(string key, byte[] value)
        {
            if (value == null)
                return false;

            var result = await memcachedClient.AppendAsync(key, value);

            return result;
        }

        public void Dispose()
        {
            memcachedCluster.Dispose();
        }
    }
}
