using System;
using System.Threading.Tasks;

namespace MyApp {
    class Program {

        static async Task Go3() {
            await Task.Run(() => { });
        }

        static async Task Go2() {
            await Task.Run(() => { });
        }
        static async Task Go1() {
            await Task.Run(() => { });
        }

        static void Main(string[] args) {
            Console.WriteLine("Hello World!");

            Go1().GetAwaiter().GetResult();
            Go2().GetAwaiter().GetResult();
            Go3().GetAwaiter().GetResult();
        }
    }
}