using System;
using System.Threading.Tasks;

namespace MyApp {
    class Program {

        static async Task Go3Async() {
            await Task.Run(() => { });
        }

        static async Task Go2Async() {
            await Task.Run(() => { });
        }
        static async Task Go1Async() {
            await Task.Run(() => { });
        }

        static void Main(string[] args) {
            Console.WriteLine("Hello World!");

            Go1Async().GetAwaiter().GetResult();
            Go2Async().GetAwaiter().GetResult();
            Go3Async().GetAwaiter().GetResult();
        }
    }
}