using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysProgPRVI.Server;

namespace SysProgPRVI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Server.Server server = new Server.Server(20);
            server.StartServer();
            Console.ReadLine();
            server.StopServer();
            //poziv primer 1("http://localhost:5500/city?city=Nis&state=Central%20Serbia&country=Serbia");
            //poziv primer 2("http://localhost:5500/city?city=Belgrade&state=Central%20Serbia&country=Serbia");
            // poziv primer 3("http://localhost:5500/city?city=Cairo&state=Cairo&country=Egypt");
            //poziv primer 4("http://localhost:5500/city?city=Tokyo&state=Tokyo&country=Japan");
            //poziv primer 5("http://localhost:5500/city?city=Berlin&state=Berlin&country=Germany"); 
            // poziv primer 6("http://localhost:5500/city?city=London&state=England&country=UK"); United%20Kingdom- ovo ne prolazi!!!
            // poziv primer 7("http://localhost:5500/city?city=New%20York&state=New%20York&country=USA"); 
        }
    }
}
