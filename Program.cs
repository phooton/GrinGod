using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace gringod
{
    class Program
    {
        public static Dictionary<string, DateTime> clients = new Dictionary<string, DateTime>();
        public static Dictionary<string, int> saps = new Dictionary<string, int>();

        static void Main(string[] args)
        {
            var shouldExit = false;
            using (var shouldExitWaitHandle = new ManualResetEvent(shouldExit))
            using (var listener = new HttpListener())
            {
                Console.CancelKeyPress += (
                    object sender,
                    ConsoleCancelEventArgs e
                ) =>
                {
                    e.Cancel = true;
                    /*
                    this here will eventually result in a graceful exit
                    of the program
                     */
                    shouldExit = true;
                    shouldExitWaitHandle.Set();
                };

                listener.Prefixes.Add("http://*:80/");

                listener.Start();
                Console.WriteLine("Server listening at port 80");

                /*
                This is the loop where everything happens, we loop until an
                exit is requested
                 */
                while (!shouldExit)
                {
                    /*
                    Every request to the http server will result in a new
                    HttpContext
                     */
                    var contextAsyncResult = listener.BeginGetContext(
                        (IAsyncResult asyncResult) =>
                        {
                            var context = listener.EndGetContext(asyncResult);
                            var ip = context.Request.RemoteEndPoint.Address;

                            Console.WriteLine();
                            Console.Write(ip + " ");
                            Console.Write(context.Request.Url.Host + " ");
                            Console.Write(context.Request.RawUrl);
                           

                            string message = "";

                            DateTime lastSeen = DateTime.MinValue;

                            if (clients.ContainsKey(ip.ToString()))
                                lastSeen = clients[ip.ToString()];

                            if (!saps.ContainsKey(ip.ToString()) || (saps.ContainsKey(ip.ToString()) && saps[ip.ToString()] < 100))
                            {
                                if (context.Request.Url.Host.Contains("gringod.info"))
                                {
                                    if (lastSeen.AddMinutes(10) < DateTime.Now)
                                    {
                                        Random r = new Random((int)DateTime.Now.Ticks);

                                        decimal amount = ((decimal)(r.NextDouble() * 100));

                                        clients[ip.ToString()] = DateTime.Now;

                                        if (!saps.ContainsKey(ip.ToString()))
                                            saps[ip.ToString()] = 0;
                                        else if (saps[ip.ToString()] > 50)
                                            amount /= saps[ip.ToString()];

                                        message = "The god is smiling on you today. Sending " + amount.ToString("0.000") + " grin.\n";

                                        try
                                        {
                                            var p = Process.Start(new ProcessStartInfo("/home/gringod/grin/target/release/grin", string.Format("wallet send -d \"http://{0}:13415\" {1:F2}", ip.ToString(), amount))
                                            {
                                                CreateNoWindow = true,
                                                RedirectStandardError = true,
                                                RedirectStandardOutput = true,
                                                StandardErrorEncoding = Encoding.ASCII,
                                                StandardOutputEncoding = Encoding.ASCII,
                                                UseShellExecute = false
                                            });

                                            for (int i = 0; i < 60; i++)
                                            {
                                                if (p.HasExited)
                                                    break;

                                                Task.Delay(1000).Wait();
                                            }

                                            int l1 = message.Trim().Length;
                                            message += p.StandardError.ReadToEnd();
                                            message += p.StandardOutput.ReadToEnd();
                                            int l2 = message.Trim().Length;

                                            if (l2-l1 < 5)
                                            {
                                                saps[ip.ToString()] += 10;
                                                Console.Write(" OK");
                                            }
                                            else
                                            {
                                                saps[ip.ToString()]++;
                                                Console.Write(" FAIL");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.Write(" EXCEPTION");
                                            message = "Sorry, god has an error :( " + ex.Message;
                                        }
                                    }
                                    else
                                        message = "Of what awaits the sin of greed, For those who take, but do not earn, Must pay most dearly in their turn. Wait " + (TimeSpan.FromMinutes(10) - (DateTime.Now - clients[ip.ToString()])).TotalMinutes.ToString("0.0") + " minutes. \n";
                                }
                                else
                                    message = "Naughty naughty boy";

                            }
                            else
                                message = "Greedy eh?";

                            /*
                            Use s StreamWriter to write text to the response
                            stream
                             */
                            using (var writer =
                                new StreamWriter(context.Response.OutputStream)
                            )
                            {
                                writer.WriteLine(message);
                            }

                        },
                        null
                    );

                    /*
                    Wait for the program to exit or for a new request 
                     */
                    WaitHandle.WaitAny(new WaitHandle[]{
                        contextAsyncResult.AsyncWaitHandle,
                        shouldExitWaitHandle
                    });
                }

                listener.Stop();
                Console.WriteLine("Server stopped");
            }
        }


    }
}
