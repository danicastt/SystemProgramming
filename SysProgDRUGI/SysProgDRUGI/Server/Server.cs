using SysProgDRUGI.IQAirAPI;
using SysProgDRUGI.Server;
using SysProgDRUGI.Models;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


/*
 * Primer poziva serveru dat u tekstu zadatka: 
http://api.airvisual.com/v2/city?city=Los%20Angeles&state=California&country=USA&key={
 {YOUR_API_KEY}} 

* MOJ API KLJUC: e28b30be-f201-4a07-a74b-0606f9382c12

 */

namespace SysProgDRUGI.Server
{
    internal class Server
    {
        private readonly HttpListener listener;
        private readonly Cache cache;
        private static IQAirService api;
        private readonly string TAG = "[Server]";

        public Server(int cacheSize)
        {
            cache = new Cache(cacheSize);
            api = new IQAirService();
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5500/");
        }

        // ASYNC start: prihvatanje konekcija bez blokiranja niti
        public async Task StartServerAsync(CancellationToken ct = default)
        {
            listener.Start();
            Logger.Info(TAG, "Pokrenuli ste server! Napravite i pošaljite poziv oblika: http://localhost:5500/city?city={city}&state={state}&country={country}");

            while (listener.IsListening && !ct.IsCancellationRequested)
            {
                // čeka novi zahtev asinhrono
                var context = await listener.GetContextAsync();
                // obradi svaki zahtev u posebnom tasku
                _ = HandleRequestAsync(context);
            }
        }

        public void StopServer() => listener.Stop();

        //parser query parametara
        private string ParseHTTP(string rawUrl, string key)
        {
            int qi = rawUrl.IndexOf("?");
            if (qi == -1) return null;

            string qs = rawUrl.Substring(qi + 1).Replace("%20", " ");
            var parts = qs.Split('&');
            foreach (var p in parts)
            {
                var kv = p.Split('=');
                if (kv.Length == 2 && kv[0] == key) return kv[1];
            }
            return null;
        }

        // CORS (dozvoli frontendu na drugom portu/originu)
        private void AddCors(HttpListenerResponse res)
        {
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        }

        // ASYNC handler 
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // Preflight (OPTIONS)
            if (req.HttpMethod == "OPTIONS")
            {
                AddCors(res);
                res.StatusCode = 200;
                res.OutputStream.Close();
                return;
            }

            AddCors(res);
            res.ContentType = "application/json; charset=utf-8";

            string url = req.RawUrl.ToLower();

            // favicon
            if (url == "/favicon.ico")
            {
                await WriteResponseAsync(context, HttpStatusCode.NoContent, "{\"message\":\"No favicon\"}");
                return;
            }

            // validacija parametara
            string city = ParseHTTP(url, "city");
            if (string.IsNullOrWhiteSpace(city))
            {
                Logger.Error(TAG, "City parametar je null!");
                await WriteResponseAsync(context, HttpStatusCode.BadRequest, "{\"error\":\"City parametar je null\"}");
                return;
            }

            string state = ParseHTTP(url, "state");
            if (string.IsNullOrWhiteSpace(state))
            {
                Logger.Error(TAG, "State parametar je null!");
                await WriteResponseAsync(context, HttpStatusCode.BadRequest, "{\"error\":\"State parametar je null\"}");
                return;
            }

            string country = ParseHTTP(url, "country");
            if (string.IsNullOrWhiteSpace(country))
            {
                Logger.Error(TAG, "Country parametar je null!");
                await WriteResponseAsync(context, HttpStatusCode.BadRequest, "{\"error\":\"Country parametar je null\"}");
                return;
            }

            // Cache - API - odgovor
            string result;
            HttpStatusCode code;

            var cached = cache.returnResponse(url);
            if (cached != null)
            {
                Logger.Info(TAG, "[HandleRequest] [Cache Hit] " + url);
                result = cached;
                code = HttpStatusCode.OK;
            }
            else
            {
                Logger.Info(TAG, "[HandleRequest] [Cache Miss] " + url);
                try
                {
                    // KORISTI ASINHRONI POZIV KA API-JU:
                    result = await api.vratiZagadjenostGradaAsync(city, state, country);
                    cache.addToCache(url, result);
                    code = HttpStatusCode.OK;
                }
                catch (Exception e)
                {
                    Logger.Error(TAG, "[IQAirApi] " + e.Message);
                    result = "{\"status\":\"error\",\"message\":\"" + EscapeForJson(e.Message) + "\"}";
                    code = HttpStatusCode.NotFound;
                }
            }

            await WriteResponseAsync(context, code, result);
        }

        // JSON escape za poruke
        private static string EscapeForJson(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // ASYNC upis odgovora
        private async Task WriteResponseAsync(HttpListenerContext context, HttpStatusCode code, string result)
        {
            AddCors(context.Response);

            context.Response.StatusCode = (int)code;
            byte[] buffer = Encoding.UTF8.GetBytes(result ?? "{}");
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;

            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        // (opciono) util ako treba iz konzole
        public void preradiRequestString(string s)
        {
            string url = s;
            var done = new ManualResetEvent(false);
            string result = null;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    result = cache.returnResponse(url);
                    Logger.Info(TAG, "[preradiRequestString] [Cache Hit] " + url);
                }
                catch (ArgumentException e)
                {
                    Logger.Error(TAG, e.Message);
                    string city = ParseHTTP(url, "city");
                    string state = ParseHTTP(url, "state");
                    string country = ParseHTTP(url, "country");

                    // sync fallback
                    result = api.vratiZagadjenostGrada(city, state, country);
                    cache.addToCache(url, result);
                }

                Logger.Info(TAG, result ?? "(null)");
                done.Set();
            });
            done.WaitOne();
        }
    }
}

//RADJENO BEZ POVEZIVANJA NA FONTEND (ako nam se trazi samo server deo)
// 
//using SysProgDRUGI.IQAirAPI;
//using SysProgDRUGI.Models;
//using System;
//using System.Net;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace SysProgDRUGI.Server
//{
//    internal class Server
//    {
//        private readonly HttpListener listener;
//        private readonly Cache cache;
//        private static IQAirService api;
//        private readonly string TAG = "[Server]";

//        public Server(int cacheSize)
//        {
//            cache = new Cache(cacheSize);
//            api = new IQAirService();
//            listener = new HttpListener();
//            listener.Prefixes.Add("http://localhost:5500/");
//        }

//        // ASYNC start
//        public async Task StartServerAsync(CancellationToken ct = default)
//        {
//            listener.Start();
//            Logger.Info(TAG, "Pokrenuli ste server!. Pošaljite poziv oblika: http://localhost:5500/city?city={city}&state={state}&country={country}");

//            while (listener.IsListening && !ct.IsCancellationRequested)
//            {
//                var context = await listener.GetContextAsync();   // prihvat asinhrono
//                _ = HandleRequestAsync(context);                  // Task 
//            }
//        }

//        public void StopServer() => listener.Stop();

//        // parser query parametara
//        private string ParseHTTP(string rawUrl, string key)
//        {
//            int qi = rawUrl.IndexOf("?");
//            if (qi == -1) return null;

//            string qs = rawUrl.Substring(qi + 1).Replace("%20", " ");
//            var parts = qs.Split('&');
//            foreach (var p in parts)
//            {
//                var kv = p.Split('=');
//                if (kv.Length == 2 && kv[0] == key) return kv[1];
//            }
//            return null;
//        }

//        // ASYNC handler (bez CORS-a / bez OPTIONS za front deo)
//        private async Task HandleRequestAsync(HttpListenerContext context)
//        {
//            var req = context.Request;

//            // dozvoli samo GET
//            if (!string.Equals(req.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
//            {
//                await WriteResponseAsync(context, HttpStatusCode.MethodNotAllowed,
//                    "{\"error\":\"Only GET is allowed\"}");
//                return;
//            }

//            string url = req.RawUrl.ToLowerInvariant();

//            // ignoriši favicon
//            if (url == "/favicon.ico")
//            {
//                await WriteResponseAsync(context, HttpStatusCode.NoContent, "");
//                return;
//            }

//            // validacija parametara
//            string city = ParseHTTP(url, "city");
//            if (string.IsNullOrWhiteSpace(city))
//            {
//                await WriteResponseAsync(context, HttpStatusCode.BadRequest, "{\"error\":\"City parametar je null\"}");
//                return;
//            }

//            string state = ParseHTTP(url, "state");
//            if (string.IsNullOrWhiteSpace(state))
//            {
//                await WriteResponseAsync(context, HttpStatusCode.BadRequest, "{\"error\":\"State parametar je null\"}");
//                return;
//            }

//            string country = ParseHTTP(url, "country");
//            if (string.IsNullOrWhiteSpace(country))
//            {
//                await WriteResponseAsync(context, HttpStatusCode.BadRequest, "{\"error\":\"Country parametar je null\"}");
//                return;
//            }

//            // cache - API - odgovor
//            string result;
//            HttpStatusCode code;

//            var cached = cache.returnResponse(url);
//            if (cached != null)
//            {
//                Logger.Info(TAG, "[HandleRequest] [Cache Hit] " + url);
//                result = cached;
//                code = HttpStatusCode.OK;
//            }
//            else
//            {
//                Logger.Info(TAG, "[HandleRequest] [Cache Miss] " + url);
//                try
//                {
//                    result = await api.vratiZagadjenostGradaAsync(city, state, country); // raw JSON sa API-ja
//                    cache.addToCache(url, result);
//                    code = HttpStatusCode.OK;
//                }
//                catch (Exception e)
//                {
//                    Logger.Error(TAG, "[IQAirApi] " + e.Message);
//                    result = "{\"status\":\"error\",\"message\":\"" + EscapeForJson(e.Message) + "\"}";
//                    code = HttpStatusCode.NotFound;
//                }
//            }

//            await WriteResponseAsync(context, code, result);
//        }

//        // JSON escape za poruke
//        private static string EscapeForJson(string s) =>
//            s?.Replace("\\", "\\\\").Replace("\"", "\\\"");

//        // ASYNC upis odgovora (čist raw JSON)
//        private async Task WriteResponseAsync(HttpListenerContext context, HttpStatusCode code, string result)
//        {
//            context.Response.StatusCode = (int)code;

//            // JSON Content-Type za prikaz
//            context.Response.ContentType = "application/json; charset=utf-8";

//            byte[] buffer = Encoding.UTF8.GetBytes(result ?? "{}");
//            context.Response.ContentLength64 = buffer.Length;

//            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
//            context.Response.OutputStream.Close();
//        }

//        // (opciono) util za test iz konzole
//        public void preradiRequestString(string s)
//        {
//            string url = s;
//            var done = new ManualResetEvent(false);
//            string result = null;

//            ThreadPool.QueueUserWorkItem(_ =>
//            {
//                try
//                {
//                    result = cache.returnResponse(url);
//                    Logger.Info(TAG, "[preradiRequestString] [Cache Hit] " + url);
//                }
//                catch (ArgumentException e)
//                {
//                    Logger.Error(TAG, e.Message);
//                    string city = ParseHTTP(url, "city");
//                    string state = ParseHTTP(url, "state");
//                    string country = ParseHTTP(url, "country");

//                    // sync fallback
//                    result = api.vratiZagadjenostGrada(city, state, country);
//                    cache.addToCache(url, result);
//                }

//                Logger.Info(TAG, result ?? "(null)");
//                done.Set();
//            });
//            done.WaitOne();
//        }
//    }
//}
