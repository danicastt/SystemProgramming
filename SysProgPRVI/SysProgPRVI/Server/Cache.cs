using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Specialized;

using SysProgPRVI.Models;

namespace SysProgPRVI.Server
{
    internal class Cache
    {
        
        private Dictionary<CacheableRequest, string> dictionary;


        private readonly ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
        private readonly int cacheSize;
        private readonly string TAG = "[Cache]";

        public Cache(int cacheSize = 0)
        {
            this.cacheSize = cacheSize;
            dictionary = new Dictionary<CacheableRequest, string>(cacheSize);
        }

        public void addToCache(string request, string response)
        {
            locker.EnterWriteLock();
            try
            {
                if (dictionary.Count() >= cacheSize)
                    cleanCache();

                // javlja arguemnt exception 
                dictionary.Add(new CacheableRequest(request, 0), response);
            }
            catch (ArgumentException e)
            {
                Logger.Error(TAG, $"[addToCache] {e.Message}");
            }
            finally
            {
                locker.ExitWriteLock();
            }
        }

        public string returnResponse(string request)
        {
            try
            {
                locker.EnterReadLock();
                if (dictionary.ContainsKey(new CacheableRequest(request, 0)) == true)
                {
                    string response;
                    if (dictionary.TryGetValue(new CacheableRequest(request), out response))
                    {
                        dictionary.
                                    FirstOrDefault(x => x.Key.HttpsRequest == request)
                                    .Key
                                    .incrementHit();

                    }

                    return response;
                }
                else
                    return null;

            }
            finally
            {
                locker.ExitReadLock();
            }
        }

        private void cleanCache()
        {

            try
            {
                Logger.Info(TAG, "Čiscenje keša...");

                List<CacheableRequest> hitsList = new List<CacheableRequest>(dictionary.Count);
                foreach (CacheableRequest request in dictionary.Keys)
                {
                    hitsList.Add(request);
                }


                hitsList = hitsList.OrderBy(r => r.NumOfHits).ToList(); //rastuce jer se izbacuju najredje koriscene stavke(LFU)

                int cachePart = 5; // oznacava da brisemo 1/5 kesa (retko ciscenje, a brzo oslobadjanje prostora)
                for (int i = 0; i < dictionary.Count / cachePart; ++i)
                    dictionary.Remove(hitsList[i]); // brise se od pocetka liste
            }
            finally
            {
                Logger.Info(TAG, "Keš uspešno očišćen!");
            }

        }

    }
}
