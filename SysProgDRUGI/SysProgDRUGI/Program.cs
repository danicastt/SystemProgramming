using SysProgDRUGI.Server;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysProgDRUGI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var server = new Server.Server(cacheSize: 20);

            // pokreni async server; blokiraj glavnu nit dok radi
            var runTask = server.StartServerAsync();

            Console.WriteLine("Server radi na http://localhost:5500/  (pritisni Enter za stop)");
            Console.ReadLine();

            server.StopServer();
            // sačekaj korektno gašenje petlje
            try { runTask.GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.WriteLine(ex); }
        }
    }
}
//poziv primer 1("http://localhost:5500/city?city=Nis&state=Central%20Serbia&country=Serbia");
//poziv primer 2("http://localhost:5500/city?city=Belgrade&state=Central%20Serbia&country=Serbia");
// poziv primer 3("http://localhost:5500/city?city=Cairo&state=Cairo&country=Egypt");
//poziv primer 4("http://localhost:5500/city?city=Tokyo&state=Tokyo&country=Japan");
//poziv primer 5("http://localhost:5500/city?city=Berlin&state=Berlin&country=Germany"); 
// poziv primer 6("http://localhost:5500/city?city=London&state=England&country=UK"); United%20Kingdom- ovo ne prolazi!!!
// poziv primer 7("http://localhost:5500/city?city=New%20York&state=New%20York&country=USA"); 



//BEZ FRONTEND DELA!!!
//
//using SysProgDRUGI.Server;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace SysProgDRUGI
//{
//    internal class Program
//    {
//        static void Main(string[] args)
//        {
//            var server = new Server.Server(cacheSize: 20);
//            var cts = new CancellationTokenSource();

//            // omogući gašenje i na Ctrl+C
//            Console.CancelKeyPress += (s, e) =>
//            {
//                e.Cancel = true;   // spreči trenutno gašenje procesa
//                cts.Cancel();       // traži od servera da stane
//            };

//            //Console.WriteLine("Server radi na http://localhost:5500/");
//            //Console.WriteLine("Pritisni Enter ili Ctrl+C za stop...");

//            // pokreni async server i ne blokiraj ručno nit: čekaj Task ispod
//            var runTask = server.StartServerAsync(cts.Token);

//            // Enter za stop
//            Console.ReadLine();
//            cts.Cancel();

//            try
//            {
//                // sačekaj korektno gašenje petlje
//                runTask.GetAwaiter().GetResult();
//            }
//            catch (OperationCanceledException)
//            {
//                // očekivano pri gašenju
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine("Greška: " + ex);
//            }
//            finally
//            {
//                server.StopServer();
//                Console.WriteLine("Server zaustavljen.");
//            }
//        }
//    }
//}
