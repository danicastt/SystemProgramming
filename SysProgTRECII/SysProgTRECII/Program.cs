using System;
using Serilog;
using SysProgTRECII.Web;
using SysProgTRECII.Services;
using System.Reactive.Linq;


namespace SysProgTRECII
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            // ubacen API kljuc:
            const string API_KEY = "RGcJXvUT27JMyNMTdJeAA9h4l5S11Q4i";

            const int port = 5062;
            using var server = new ApiServer(port, API_KEY);
            server.Start();

            Console.WriteLine();
            Console.WriteLine($"API pokrenut: http://localhost:{port}/api/most-popular?feed=viewed&period=7&limit=40");
            Console.WriteLine("Pritisni Enter za izlaz…");
            Console.ReadLine();
        }
    }
}



//using System;
//using System.Linq;
//using System.Reactive.Linq;
//using System.Reactive.Threading.Tasks; // za ToTask()
//using System.Threading.Tasks;
//using SysProgTRECII.Services;

//namespace SysProgTRECII
//{
//    public static class Program
//    {
//        // hard-kodovan API ključ:
//        private const string API_KEY = "RGcJXvUT27JMyNMTdJeAA9h4l5S11Q4i";

//        public static async Task Main(string[] args)
//        {
//            // Parametri (po difoltu): feed=viewed, period=7, limit=40, parallel=4
//            string feed = args.Length > 0 ? args[0].ToLowerInvariant() : "viewed"; // viewed|shared|emailed
//            int period = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 7; // 1|7|30
//            int limit = args.Length > 2 && int.TryParse(args[2], out var l) ? l : 40;
//            int parallel = args.Length > 3 && int.TryParse(args[3], out var par) ? par : 4;

//            if (feed != "viewed" && feed != "shared" && feed != "emailed") feed = "viewed"; // ako se ne postave odgovarajuci argumenti, ovde se normalizuju
//            if (period != 1 && period != 7 && period != 30) period = 7;
//            if (limit < 1) limit = 1; if (limit > 100) limit = 100;
//            if (parallel < 1) parallel = 1;

//            Console.WriteLine($"START (feed={feed}, period={period}, limit={limit}, parallel={parallel})\n");

//            using var news = new NewsClient(API_KEY, feed);
//            ITextSentiment sentiment = new TextSentiment();

//            var pipeline =
//                news.StreamMostPopular(period)
//                    .Take(limit)
//                    .Select(item =>
//                        Observable.FromAsync(async () =>
//                        {
//                            var text = $"{item.Title} {item.Abstract}".Trim();
//                            var (prob, isPos) = await sentiment.AnalyzeAsync(text);
//                            return new
//                            {
//                                item.Title,
//                                item.Abstract,
//                                item.Url,
//                                item.Published,
//                                Probability = prob,
//                                IsPositive = isPos
//                            };
//                        }))
//                    .Merge(parallel);

//            var list = await pipeline.ToList().ToTask();

//            // Ispis pojedinačno
//            int i = 1;
//            foreach (var a in list.OrderByDescending(x => x.Published))
//            {
//                var lab = a.IsPositive ? "positive" : "negative";
//                Console.WriteLine($"{i,2}. [{a.Published:yyyy-MM-dd}] {a.Title}");
//                if (!string.IsNullOrWhiteSpace(a.Abstract))
//                    Console.WriteLine($"    {a.Abstract}");
//                Console.WriteLine($"    sentiment: {lab}  •  score={a.Probability:F3}");
//                Console.WriteLine($"    link: {a.Url}\n");
//                i++;
//            }

//            // Rezime
//            var pos = list.Count(x => x.IsPositive);
//            var neg = list.Count - pos;
//            var avg = list.Count > 0 ? list.Average(x => x.Probability) : 0;
//            Console.WriteLine("===== REZIME =====");
//            Console.WriteLine($"Ukupno: {list.Count} • Pozitivnih: {pos} • Negativnih: {neg} • Prosečan score: {avg:F3}");
//        }
//    }
//}

