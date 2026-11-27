using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kentavr.SheetsToSO.Cli
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using (var handler = new HttpClientHandler())
                return await CliApp.RunAsync(args, Console.Out, Console.Error, Environment.GetEnvironmentVariable, handler);
        }
    }
}
