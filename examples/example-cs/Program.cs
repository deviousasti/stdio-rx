using System;
using System.Reactive.Linq;
using System.Reactive.Linq.Stdio;

namespace example_cs
{
    class Program
    {
        static void Main(string[] args)
        {
            var couch = StdioObservable.Where("couchdb.cmd");
            using 
            (
                StdioObservable.Create(couch)
                .Retry()
                .Subscribe(Console.WriteLine)
            )
            {
                Console.ReadLine();
            }
        }
    }
}
