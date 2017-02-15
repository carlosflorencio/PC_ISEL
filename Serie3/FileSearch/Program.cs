using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileSearch {

    class Program {

        static void Main(string[] args) {
            var folder = @"C:\Windows\System32";
            var cts = new CancellationTokenSource();


            var task = FileSearch.Find(folder, "dll", "teste", cts.Token);

            try {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var result = task.Result; // wait for the result
                watch.Stop();

                Console.WriteLine("Time taken: {0}ms", watch.Elapsed.TotalMilliseconds);
                Console.WriteLine("Total Files Found: " + result.totalFiles);
                Console.WriteLine("Extension Files Found: " + result.totalFilesWithExtension);
                Console.WriteLine("Files Matching: ");

                foreach (var resultFile in result.files)
                {
                    Console.WriteLine(resultFile);
                }

            } catch (AggregateException ex) {
                ex.Handle(e => {
                    Console.WriteLine("Exception: " + e.Message);
                    return true;
                });
            }
            
            Console.WriteLine("Press enter..");
            Console.ReadKey();
        }

    }

}