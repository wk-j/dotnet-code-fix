using Serilog;
using Serilog.Core;

namespace DotNetCodeFix {
    public class Log {
        public static Logger New() {
            return new LoggerConfiguration().WriteTo.Console().CreateLogger();
        }
    }
}