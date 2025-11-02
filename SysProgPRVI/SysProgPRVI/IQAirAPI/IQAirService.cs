using System;
//using System.Collections.Generic;
using System.IO;
//using System.Linq;
using System.Net;
using System.Net.Http;
//using System.Text;
using System.Threading;

using SysProgPRVI.Server;

namespace SysProgPRVI.IQAirAPI
{
    internal class IQAirService
    {

        private readonly string api_key = "e28b30be-f201-4a07-a74b-0606f9382c12"; //kljuc generisan sa IQAir sajta

        private readonly string TAG = "[IqAirService]";

        public IQAirService() { }

        public string vratiZagadjenostGrada(string city, string state, string country)
        {
            string requestUri = $"http://api.airvisual.com/v2/city" +
                                $"?city={Uri.EscapeDataString(city)}" +
                                $"&state={Uri.EscapeDataString(state)}" +
                                $"&country={Uri.EscapeDataString(country)}" +
                                $"&key={api_key}";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Method = "GET";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
