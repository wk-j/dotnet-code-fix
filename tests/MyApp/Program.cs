using System;
using System.Threading.Tasks;

namespace MyApp
{
    class Program
    {
        static async Task X()
        {
            await Task.Run(() => { });
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
