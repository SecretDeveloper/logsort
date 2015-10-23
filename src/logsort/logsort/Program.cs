using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CliParse;

namespace logsort
{
    class Program
    {
        [CliParse.ParsableClass("Sort Config")]
        public class SortConfig:CliParse.Parsable
        {
            [CliParse.ParsableArgument("path", ShortName = 'p', ImpliedPosition = 0, Required = true)]
            public string Path { get; set; }

            [CliParse.ParsableArgument("out", ShortName = 'o', DefaultValue = "logsort_output.txt")]
            public string OutputFile { get; set; }
             
        }

        public class DuplicateKeyComparer<TKey>:IComparer<TKey> where TKey : IComparable
        {
            public int Compare(TKey x, TKey y)
            {
                int result = x.CompareTo(y);

                if (result == 0) return 1;
                
                return result;
            }
        }

        static void Main(string[] args)
        {

#if DEBUG
            Debugger.Launch();
#endif
            try
            {
                var sortConfig = new SortConfig();
                var parseResult = sortConfig.CliParse(args);
                if (!parseResult.Successful || parseResult.ShowHelp)
                {
                    Console.WriteLine(sortConfig.GetHelpInfo());
                    return;
                }

                sortConfig.OutputFile = Path.Combine(sortConfig.Path, sortConfig.OutputFile);
                
                var logentries = new SortedList<string, string>(new DuplicateKeyComparer<string>());

                string key = "";
                var sb = new StringBuilder();
                foreach (var file in Directory.EnumerateFiles(sortConfig.Path))
                {
                    if(file.Equals(sortConfig.OutputFile,StringComparison.InvariantCultureIgnoreCase)) continue; // skip our output file in case it already exists.

                    foreach (var line in File.ReadLines(file))
                    {
                        if (IsStartOfEntry(line))
                        {
                            // add entry
                            logentries.Add(key, sb.ToString());
                            
                            sb = new StringBuilder();
                            // set new key
                            key = GetKeyFromLine(line);
                            sb.AppendLine(line.Trim());
                            continue;
                        }
                        sb.AppendLine("    " + line.Trim());
                    }

                    logentries.Add(key, sb.ToString());
                    sb = new StringBuilder();
                }
                
                File.Delete(sortConfig.OutputFile);
                File.WriteAllLines(sortConfig.OutputFile, logentries.Values);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static string GetKeyFromLine(string line)
        {
            if (line.Length < 23) return "";

            return line.Substring(0, 23);
        }

        private static bool IsStartOfEntry(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (string.IsNullOrEmpty(line.TrimStart())) return false;

            return line.TrimStart()[0] == '2';// entries start with a timestamp '2015....'
        }
    }
}
