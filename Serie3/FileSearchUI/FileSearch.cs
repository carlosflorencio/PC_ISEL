using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileSearchUI {

    class SearchResult {

        public int totalFiles;
        public int totalFilesWithExtension;
        public ConcurrentBag<String> files = new ConcurrentBag<string>();

    }

    class FileSearch {

        // Caller must catch the AggregateException
        public static Task<SearchResult> Find(string folder, string ext,
            string sequence, CancellationToken token, IProgress<CustomProgress> progress) {
            var task = Task.Factory.StartNew<SearchResult>(() => {
                var result = new SearchResult();

                string[] files = null;
                try {
                    // get all the files in that directory (& subdirectories) to count
                    files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
                } catch (UnauthorizedAccessException) {
                    // if we try to access a subdirectory which we don't have permission
                    // forget subdirectories, just search in the root folder
                    files = Directory.GetFiles(folder);
                }

                result.totalFiles = files.Length; // save the count

                // filter the files by the extension we want (toArray because we want the size)
                var filesWithExtension = files.Where(f => f.EndsWith("." + ext)).ToArray();
                result.totalFilesWithExtension = filesWithExtension.Length;

                // if canceled at this point, we dont need to read all the files
                token.ThrowIfCancellationRequested();

                // used to throttled the IO concurrency in case we have many files
                SemaphoreSlim semaphore = new SemaphoreSlim(20);

                // read the contents of all the files
                // we assume there are multiple cores on this system
                // if not, we should do a sync version
                Parallel.ForEach(filesWithExtension, (file, loop) => {
                    semaphore.Wait(token); // cancel the wait if token is signaled

                    var contains = false;

                    using (StreamReader reader = File.OpenText(file)) {
                        string line = null;
                        while ((line = reader.ReadLine()) != null) {
                            if (line.Contains(sequence)) {
                                contains = true;
                                result.files.Add(file);
                                break;
                            }
                        }
                    }

                    var state = new CustomProgress(result.totalFilesWithExtension, contains ? file : null);
                    progress.Report(state); // will run in the ui thread

                    if (token.IsCancellationRequested) {
                        loop.Stop(); // cancel all the remaining loop tasks
                    }

                    semaphore.Release();
                });


                return result;
            }, token);

            return task;
        }

    }

}