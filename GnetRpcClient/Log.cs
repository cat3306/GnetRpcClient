// A simple logger class that uses Console.WriteLine by default.
// Can also do Logger.LogMethod = Debug.Log for Unity etc.
// (this way we don't have to depend on UnityEngine.DLL and don't need a
//  different version for every UnityEngine version here)
using System;
using Serilog;
using Serilog.Core;
namespace GnetRpcClient
{
    public class Log
    {
        public Logger logger;
        public Log()
        {
            logger = new LoggerConfiguration()
       .WriteTo.Console(
           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
       .CreateLogger();
        }
    }
}
