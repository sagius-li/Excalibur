using OCG.DataService.Contract;
using System;
using System.Runtime.Caching;

namespace OCG.DataService.Repo.MIMResource
{
    public class ResourceCache : ICache
    {
        private readonly ObjectCache cache = MemoryCache.Default;

        private readonly int timeout = 0;

        public ResourceCache(int timeoutInMinutes)
        {
            this.timeout = timeoutInMinutes;
        }

        public bool Contains(string token)
        {
            return this.cache.Contains(token);
        }

        public string Set<T>(T value)
        {
            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(timeout)
            };

            Guid guid = Guid.NewGuid();

            this.cache.Add(guid.ToString(), value, policy);

            return guid.ToString();
        }

        public void Set<T>(string token, T value)
        {
            CacheItemPolicy policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(timeout)
            };

            this.cache.Add(token, value, policy);
        }

        public bool TryGet<T>(string token, out T value)
        {
            try
            {
                value = (T)this.cache.Get(token);
                return value == null ? false : true;
            }
            catch
            {
                value = default(T);
                return false;
            }
        }
    }
}
