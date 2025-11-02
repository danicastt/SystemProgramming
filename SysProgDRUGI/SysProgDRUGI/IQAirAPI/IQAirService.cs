using System;
using System.Configuration; // dodata referenca
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SysProgDRUGI.IQAirAPI
{
    internal class IQAirService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private const string BaseUrl = "http://api.airvisual.com/v2";
        private readonly string _apiKey;
        private const string TAG = "[IqAirService]";

        public IQAirService()
        {
            // ako je kljuc podesavan u App.config
            _apiKey = ConfigurationManager.AppSettings["IQAirApiKey"];

            // ako nema u App.config 
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _apiKey = "e28b30be-f201-4a07-a74b-0606f9382c12";
            }

            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SysProgPRVI/1.0 (+http)");
        }

        public async Task<string> vratiZagadjenostGradaAsync(string city, string state, string country, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("city je obavezan", nameof(city));
            if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("state je obavezan", nameof(state));
            if (string.IsNullOrWhiteSpace(country)) throw new ArgumentException("country je obavezan", nameof(country));
            if (string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("Nedostaje API ključ (IQAirApiKey).");

            var url = $"{BaseUrl}/city" +
                      $"?city={Uri.EscapeDataString(city)}" +
                      $"&state={Uri.EscapeDataString(state)}" +
                      $"&country={Uri.EscapeDataString(country)}" +
                      $"&key={_apiKey}";

            //ovaj deo je kompatibilan za C# 8 sintaksu
            //using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            //var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            //if (!resp.IsSuccessStatusCode)
            // throw new Exception($"API {resp.StatusCode}: {body}");

            //return body;
            
            //jer nam treba kompatibilnost za sintakse nize od C# 8

            using (var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"API {resp.StatusCode}: {body}");

                return body; //raw json
            }
        }

        // sync shim (ako negde još koristiš stari sync kod)
        public string vratiZagadjenostGrada(string city, string state, string country)
            => vratiZagadjenostGradaAsync(city, state, country).GetAwaiter().GetResult();
    }
}
