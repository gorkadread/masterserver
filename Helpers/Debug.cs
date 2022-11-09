using System;
using System.Globalization;
using System.Threading;

namespace Helpers {
    class Debug {
        static Debug debug;

        public static Debug Instance
        {
            get
            {
                if (debug == null) {
                    debug = new Debug();
                }

                return debug;
            }
        }

        public void Write(string message) {
            var threadName = Thread.CurrentThread.Name;

            if (threadName == null) {
                threadName = "Main";
            }

            Console.WriteLine(DateTime.Now.ToString("hh:mm:ss.fff tt", CultureInfo.InvariantCulture) + " " + threadName + ": " + message);
        }
    }
}
