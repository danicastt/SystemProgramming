using SysProgPRVI.IQAirAPI;
using SysProgPRVI.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Text;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;


/*
 * Primer poziva serveru dat u tekstu zadatka: 
http://api.airvisual.com/v2/city?city=Los%20Angeles&state=California&country=USA&key={
 {YOUR_API_KEY}} 

* MOJ API KLJUC: e28b30be-f201-4a07-a74b-0606f9382c12

 */
namespace SysProgPRVI.Server
{
    internal class Server
    {
        private HttpListener listener;
        private Cache cache;
        private static IQAirAPI.IQAirService api;
        private readonly string TAG = "[Server]";

        public Server(int cacheSize)
        {
            cache = new Cache(cacheSize);
            api = new IQAirService();
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5500/");
        }

        public void StartServer()
        {
            listener.Start();
            Logger.Info(TAG, "Pokrenuli ste server! Napravite i posaljite zahtev oblika: http://localhost:5500/city?city={city}&state={state}&country={country}");

            while (listener.IsListening)
            {
                var context = listener.GetContext();
                preradiRequest(context);
            }
        }

        public void StopServer() => listener.Stop();

        private string ParseHTTP(string RawUrl, string key)
        {
            int IndexOfStart = RawUrl.IndexOf("?");
            if (IndexOfStart == -1) return null;

            string QueryString = RawUrl.Substring(IndexOfStart + 1);
            QueryString = QueryString.Replace("%20", " ");
            string[] deloviurl = QueryString.Split('&');

            foreach (var deoUrl in deloviurl)
            {
                string[] ParametarUrl = deoUrl.Split('=');
                if (ParametarUrl.Length == 2 && ParametarUrl[0] == key)
                    return ParametarUrl[1];
            }
            return null;
        }

        // DEO ZA FRONTEND: helper za CORS header-e 
        private void AddCors(HttpListenerResponse res)
        {
            res.Headers["Access-Control-Allow-Origin"] = "*"; //dozvoli svim origin-ima (i front-u i back-u)
            res.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        }

        private void preradiRequest(HttpListenerContext context)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var req = context.Request;
                var res = context.Response;

                // CORS preflight (OBAVEZNO da bi se dobila dozvola od browser-a pre slanja pravog GET-a)
                if (req.HttpMethod == "OPTIONS")
                {
                    AddCors(res);
                    res.StatusCode = 200;
                    res.OutputStream.Close();
                    return; // sledeći zahtev
                }

                // CORS headeri za regularne odgovore
                AddCors(res);
                res.ContentType = "application/json; charset=utf-8";

                string url = req.RawUrl.ToLower();

                if (url == "/favicon.ico")
                {
                    vratiOdgovorKorisniku(context, HttpStatusCode.NoContent, "{\"message\":\"No favicon\"}");
                    return;
                }
                //ostatak obrade
                string city = ParseHTTP(url, "city");
                if (string.IsNullOrEmpty(city))
                {
                    Logger.Error(TAG, "City parametar je null!");
                    vratiOdgovorKorisniku(context, HttpStatusCode.BadRequest, "{\"error\":\"City parametar je null\"}");
                    return;
                }

                string state = ParseHTTP(url, "state");
                if (string.IsNullOrEmpty(state))
                {
                    Logger.Error(TAG, "State parametar je null!");
                    vratiOdgovorKorisniku(context, HttpStatusCode.BadRequest, "{\"error\":\"State parametar je null\"}");
                    return;
                }

                string country = ParseHTTP(url, "country");
                if (string.IsNullOrEmpty(country))
                {
                    Logger.Error(TAG, "Country parametar je null!");
                    vratiOdgovorKorisniku(context, HttpStatusCode.BadRequest, "{\"error\":\"Country parametar je null\"}");
                    return;
                }

                string result;
                HttpStatusCode code;

                if ((result = cache.returnResponse(url)) != null)
                {
                    Logger.Info(TAG, "[preradiRequest] [Cache Hit] " + url);
                    code = HttpStatusCode.OK;
                }
                else
                {
                    Logger.Error(TAG, $"[vratiResponse] {url} se ne nalazi u kesu!");
                    try
                    {
                        result = api.vratiZagadjenostGrada(city, state, country);
                        cache.addToCache(url, result);
                        code = HttpStatusCode.OK;
                    }
                    catch (Exception e)
                    {
                        Logger.Error(TAG, "[IQAirApi]" + e.Message);
                        // vrati JSON gresku (da front moze lako da je parsira)
                        result = "{\"status\":\"error\",\"message\":\"" + EscapeForJson(e.Message) + "\"}";
                        code = HttpStatusCode.NotFound;
                    }
                }

                Logger.Info(TAG, "Korisniku su vraceni rezultati: " + result);
                vratiOdgovorKorisniku(context, code, result);
            });
        }

        //pomoć da se ne pokvari JSON kada je poruka izuzetka
        private static string EscapeForJson(string s) =>
            s?.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private void vratiOdgovorKorisniku(HttpListenerContext context, HttpStatusCode code, string result)
        {
            // CORS osiguranje (ako je neko drugi pozvao ovu metodu)
            AddCors(context.Response);

            context.Response.StatusCode = (int)code;
            byte[] buffer = Encoding.UTF8.GetBytes(result ?? "{}");

            // vraćamo JSON
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;

            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();
            context.Response.OutputStream.Close();
        }

        public void preradiRequestString(string s)
        {
            string url = s;
            var done = new ManualResetEvent(false);
            string result;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    result = cache.returnResponse(url);
                    Logger.Info(TAG, "[preradiRequest] [Cache Hit] " + url);
                }
                catch (ArgumentException e)
                {
                    Logger.Error(TAG, e.Message);
                    string city = ParseHTTP(url, "city");
                    string state = ParseHTTP(url, "state");
                    string country = ParseHTTP(url, "country");

                    result = api.vratiZagadjenostGrada(city, state, country);
                    cache.addToCache(url, result);
                }

                Logger.Info(TAG, result);
                done.Set();
            });
            done.WaitOne();
        }
    }
}

// UREDJENO BEZ FRONTENDA

//namespace SysProgPRVI.Server
//{
//    internal class Server
//    {
//        private HttpListener listener;
//        private Cache cache;
//        private static IQAirApi.IQAirService api;
//        private readonly string TAG = "[Server]";
//
//
//        public Server(int velicinaKesa)
//        {
//            cache = new Cache(velicinaKesa);
//            api = new IQAirService();
//            listener = new HttpListener();

//            listener.Prefixes.Add("http://localhost:5500/");
//        }

//        public void StartServer()
//        {
//            listener.Start();

//            Logger.Info(TAG, "Server pokrenut. Posaljite zahtev oblika: http://localhost:5500/city?city={city}&state={state}&country={country}");

//            while(listener.IsListening)
//            {
//                var context = listener.GetContext();

//                preradiRequest(context);
//            }

//        }

//        public void StopServer()
//        {
//            listener.Stop();

//        }

//        private string ParseHTTP(string RawUrl, string key)
//        {

//            int IndexOfStart = RawUrl.IndexOf("?");


//            if (IndexOfStart == -1)
//            {
//                return null;
//            }


//            string QueryString = RawUrl.Substring(IndexOfStart + 1);
//            QueryString = QueryString.Replace("%20", " "); // to je ona 1/5 koja je i kod kesa


//            string[] deloviurl = QueryString.Split('&');

//            foreach (var deoUrl in deloviurl)
//            {
//                string[] ParametarUrl = deoUrl.Split('=');

//                if (ParametarUrl.Length == 2 && ParametarUrl[0] == key)
//                {
//                    return ParametarUrl[1]; 
//                }

//            }

//            return null; // ako nema ne vraca nist


//        }
//        private void preradiRequest(HttpListenerContext context)
//        {
//            ThreadPool.QueueUserWorkItem(_ =>
//            {
//                string url = context.Request.RawUrl.ToLower();

//                if(url == "/favicon.ico")
//                { 
//                    //Logger.Error(TAG, "Favicon.ico zahtev primljen!");
//                    vratiOdgovorKorisniku(context, HttpStatusCode.NoContent, "Nemamo ikonicu :( !");
//                    return;
//                }


//                string city = ParseHTTP(url, "city");
//                if (string.IsNullOrEmpty(city))
//                {
//                    Logger.Error(TAG, "City parametar je null!");
//                    vratiOdgovorKorisniku(context, HttpStatusCode.BadRequest, "City parametar je null!");
//                    return;
//                }

//                string state = ParseHTTP(url, "state"); ;
//                if (string.IsNullOrEmpty(state))
//                {
//                    Logger.Error(TAG, "State parametar je null!");
//                    vratiOdgovorKorisniku(context, HttpStatusCode.BadRequest, "State parametar je null!");
//                    return;
//                }

//                string country = ParseHTTP(url, "country");
//                if (string.IsNullOrEmpty(country))
//                {
//                    Logger.Error(TAG, "Country parametar je null!");
//                    vratiOdgovorKorisniku(context, HttpStatusCode.BadRequest, "Country parametar je null!");
//                    return;
//                }

//                string result;
//                HttpStatusCode code;

//                //string query = city+state+country; // cuva u cache


//                if ((result = cache.vratiResponse(url)) != null)
//                {
//                    Logger.Info(TAG, "[preradiRequest] [Cache Hit]" + url);
//                    code = HttpStatusCode.OK;

//                }
//                else
//                {
//                    Logger.Error(TAG, $"[vratiResponse] {url} se ne nalazi u kesu!");

//                    try
//                    {

//                        result = api.vratiZagadjenostGrada(city, state, country);
//                        cache.ubaciUKes(url, result);//moze da baci exception ali se hvata u cache-u
//                                                    

//                        code = HttpStatusCode.OK;
//                    }
//                    catch (Exception e)//ako API vrati gresku
//                    {
//                        Logger.Error(TAG, "[IQAirApi]"+e.Message);
//                        result = "ERROR:"+e.Message;
//                        code = HttpStatusCode.NotFound;
//                    }
//                }
//                Logger.Info(TAG,"Korisniku su vraceni rezultati:  "+ result);
//                vratiOdgovorKorisniku(context, code, result);


//            });
//        }

//        private void vratiOdgovorKorisniku(HttpListenerContext context,HttpStatusCode code,string result)
//        {
//            context.Response.StatusCode = (int)code;
//            byte[] buffer = Encoding.UTF8.GetBytes(result);
//            context.Response.ContentType = "text/plain";
//            context.Response.ContentLength64 = buffer.Length;

//            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
//            context.Response.OutputStream.Flush();
//            context.Response.OutputStream.Close();
//        }



//        public void preradiRequestString(string s)
//        {
//            //otprilike ovako da izgleda

//            string url = s;
//            var done = new ManualResetEvent(false);
//            string result;
//            ThreadPool.QueueUserWorkItem(_ =>
//            {
//                try
//                {
//                    result = cache.vratiResponse(url);
//                    Logger.Info(TAG, "[preradiRequest] [Cache Hit]" + url);
//                }
//                catch (ArgumentException e)
//                {
//                    Logger.Error(TAG, e.Message);

//                    string city = ParseHTTP(url, "city");
//                    string state = ParseHTTP(url, "state"); ;
//                    string country = ParseHTTP(url, "country");

//                    result = api.vratiZagadjenostGrada(city, state, country);//baca exception ako
//                    //je request los, mora da se preradi

//                    cache.ubaciUKes(url, result);
//                }

//                Logger.Info(TAG, result);
//                done.Set();
//            });
//            done.WaitOne();
//        }
//    }