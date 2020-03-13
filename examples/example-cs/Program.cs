using System;
using System.Reactive.Linq;
using System.Reactive.Linq.Stdio;

namespace example_cs
{
    class Program
    {
        static void Main(string[] args)
        {
            
            using
            (     
                //Uncomment one of the following to test
                //CouchDb()
                Wget()
            )
            {
                Console.ReadLine();
            }

            Console.WriteLine("Done.");
        }

        static IDisposable CouchDb()
        {
            var couch = StdioObservable.Where("couchdb.cmd");
            return
                StdioObservable.Create(couch)
                .Retry()
                .Subscribe(Console.WriteLine);
        }

        static IDisposable Wget()
        {
            return
                StdioObservable.Create("wget", "http://releases.ubuntu.com/18.04.4/ubuntu-18.04.4-desktop-amd64.iso -q --show-progress")
                .Timeout(TimeSpan.FromSeconds(10))
                .Subscribe(
                    text => Console.WriteLine("\r" + text), 
                    exn =>  Console.WriteLine("Download timed out")
                );
        }
    }
}
