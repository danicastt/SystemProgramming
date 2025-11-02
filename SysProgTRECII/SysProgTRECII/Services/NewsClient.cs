using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using Serilog;
using SysProgTRECII.Models;

namespace SysProgTRECII.Services
{
    public sealed class NewsClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _feed;

        public NewsClient(string apiKey, string feed)
        {
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _feed = (feed?.ToLowerInvariant()) switch
            {
                "shared" => "shared",
                "emailed" => "emailed",
                _ => "viewed"
            };

            _http = new HttpClient { BaseAddress = new Uri("https://api.nytimes.com/") };
            _http.DefaultRequestHeaders.Add("User-Agent", "NytStreamSentiment/0.1");
        }

        public string ApiKey { get; }

        public IObservable<Item> StreamMostPopular(int periodDays)
        {
            if (periodDays != 1 && periodDays != 7 && periodDays != 30)
                throw new ArgumentException("periodDays mora biti 1, 7 ili 30.");

            var url = $"svc/mostpopular/v2/{_feed}/{periodDays}.json?api-key={ApiKey}";

            return Observable
                .FromAsync(async () =>
                {
                    Log.Information("GET {Url}", url);
                    using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                    return json;
                })
                .SelectMany(raw =>
                {
                    var root = JObject.Parse(raw);
                    var results = (JArray?)root["results"] ?? new JArray();
                    var list = new List<Item>(results.Count);
                    foreach (var r in results)
                    {
                        var title = r.Value<string>("title") ?? string.Empty;
                        var abs = r.Value<string>("abstract") ?? string.Empty;
                        var link = r.Value<string>("url") ?? string.Empty;
                        var pub = DateTime.TryParse(r.Value<string>("published_date"), out var dt)
                                  ? dt : DateTime.MinValue;

                        list.Add(new Item
                        {
                            Title = title,
                            Abstract = abs,
                            Url = link,
                            Published = pub
                        });
                    }
                    return list.ToObservable();
                })
                .Retry(2)
                .Catch<Item, Exception>(ex =>
                {
                    Log.Warning(ex, "Problem sa NYT API-jem, vraćam prazan tok.");
                    return Observable.Empty<Item>();
                });
        }

        public void Dispose() => _http.Dispose();
    }
}
