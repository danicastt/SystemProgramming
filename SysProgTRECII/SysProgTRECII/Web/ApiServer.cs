using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency; // zbog scheduler-a
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SysProgTRECII.Services;

namespace SysProgTRECII.Web
{
    public sealed class ApiServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private CancellationTokenSource? _cts;
        private readonly string _apiKey;

        public ApiServer(int port, string nytApiKey)
        {
            _apiKey = nytApiKey ?? "";
            _listener.Prefixes.Add($"http://localhost:{port}/");
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener.Start();

            var firstPrefix = _listener.Prefixes.Cast<string>().FirstOrDefault() ?? "(no prefix)";
            Log.Information("API server listening at {Url}", firstPrefix);

            _ = Task.Run(() => Loop(_cts.Token));
        }

        private async Task Loop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch when (ct.IsCancellationRequested) { break; }
                _ = Handle(ctx);
            }
        }

        // minimalni parser query stringa
        private static Dictionary<string, string> ParseQuery(string? query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return dict;
            if (query.StartsWith("?")) query = query.Substring(1);

            foreach (var part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(new[] { '=' }, 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                dict[key] = val;
            }
            return dict;
        }

        private async Task Handle(HttpListenerContext ctx)
        {
            var sw = Stopwatch.StartNew();
            var url = ctx.Request.Url?.ToString() ?? "";
            var method = ctx.Request.HttpMethod ?? "UNKNOWN";

            try
            {
                // CORS (frontend)
                ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
                ctx.Response.Headers["Access-Control-Allow-Headers"] = "*";

                Log.Information("REQ {Method} {Url}", method, url);

                if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    Log.Information("OK  {Method} {Url} {Status} in {Elapsed}ms",
                        method, url, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
                    return;
                }

                var path = ctx.Request.Url?.AbsolutePath?.Trim('/') ?? "";
                if (path.Equals("api/most-popular", StringComparison.OrdinalIgnoreCase))
                {
                    var q = ParseQuery(ctx.Request.Url?.Query);

                    var feed = (q.TryGetValue("feed", out var f) && !string.IsNullOrWhiteSpace(f))
                        ? f.ToLowerInvariant() : "viewed";

                    var period = (q.TryGetValue("period", out var ps) && int.TryParse(ps, out var per)) ? per : 7;

                    var limit = (q.TryGetValue("limit", out var ls) && int.TryParse(ls, out var lim)) ? lim : 40;
                    if (limit < 1) limit = 1;
                    if (limit > 100) limit = 100;

                    var parallel = (q.TryGetValue("parallel", out var prs) && int.TryParse(prs, out var par)) ? par : 4;
                    if (parallel < 1) parallel = 1;

                    var payload = await AnalyzeMostPopular(feed, period, limit, parallel);
                    var bytes = Encoding.UTF8.GetBytes(payload);

                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.StatusCode = 200;
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    ctx.Response.Close();

                    Log.Information("OK  {Method} {Url} {Status} in {Elapsed}ms",
                        method, url, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
                }
                else
                {
                    var msg = Encoding.UTF8.GetBytes("Use GET /api/most-popular");
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.OutputStream.WriteAsync(msg, 0, msg.Length);
                    ctx.Response.Close();

                    Log.Information("404 {Method} {Url} {Status} in {Elapsed}ms",
                        method, url, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ERR {Method} {Url} in {Elapsed}ms", method, url, sw.ElapsedMilliseconds);
                try
                {
                    var bytes = Encoding.UTF8.GetBytes("{\"error\":\"server\"}");
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    ctx.Response.Close();
                }
                catch { /* ignore secondary errors */ }
            }
        }

        private async Task<string> AnalyzeMostPopular(string feed, int period, int limit, int parallel)
        {
            if (feed != "viewed" && feed != "shared" && feed != "emailed") feed = "viewed";
            if (period != 1 && period != 7 && period != 30) period = 7;

            using var news = new NewsClient(_apiKey, feed);
            ITextSentiment sentiment = new TextSentiment();

            var pipeline =
                news.StreamMostPopular(period)
                    .Take(limit)
                    // eksplicitno koristimo Rx Scheduler (Task pool)
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Select(item =>
                        Observable.FromAsync(async () =>
                        {
                            var text = $"{item.Title} {item.Abstract}".Trim();
                            var (prob, isPos) = await sentiment.AnalyzeAsync(text);
                            return new
                            {
                                title = item.Title,
                                abstract_ = item.Abstract,
                                url = item.Url,
                                published = item.Published,
                                sentiment = new { probability = prob, isPositive = isPos }
                            };
                        }))
                    // paralelna obrada više Task-ova
                    .Merge(parallel);

            var list = await pipeline.ToList().ToTask();

            var payload = new
            {
                feed,
                period,
                count = list.Count,
                items = list
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); _listener.Close(); } catch { }
        }
    }
}
