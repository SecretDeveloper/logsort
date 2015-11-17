using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CliParse;

namespace logsort
{
    class Program
    {
        [CliParse.ParsableClass("Sort Config")]
        public class SortConfig:CliParse.Parsable
        {
            [CliParse.ParsableArgument("path", ShortName = 'p', ImpliedPosition = 0, Required = true, Description = "The path to scan for log files.")]
            public string Path { get; set; }

            [CliParse.ParsableArgument("match", ShortName = 'm', DefaultValue = ".*", Description = "Regex used to filter filenames, only those which match are included.")]
            public string InputFileMatch { get; set; }

            [CliParse.ParsableArgument("keymatch", ShortName = 'k', DefaultValue = "^\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2},\\d{3}", Description = "The regex used to find the log entry identifier.  The default is a datetime at the start of a line.")]
            public string KeyMatch { get; set; }

            [CliParse.ParsableArgument("prefix", ShortName = 'f', DefaultValue = "", Description = "A prefix to prepend at the start of each log entry in the resulting output.")]
            public string EntryPrefix { get; set; }

            [CliParse.ParsableArgument("entrymatch", ShortName = 'e', DefaultValue = "", Description = "A regex used to determine if a log entry should be included in the results.")]
            public string EntryMatch { get; set; }

            [CliParse.ParsableArgument("entryinclude", ShortName = 'i', DefaultValue = "", Description = "A value which a log entry must contain in order to be included in the results.")]
            public string EntryIncludes { get; set; }
            
            [CliParse.ParsableArgument("out", ShortName = 'o', DefaultValue = "", Description = "The file path to write the results to.")]
            public string OutputFile { get; set; }

            public bool ShouldAddEntry(string entry)
            {
                if (EntryMatch == "" && EntryIncludes == "") return true;

                if (EntryIncludes != "")
                {
                    if (entry.IndexOf(EntryIncludes, StringComparison.InvariantCultureIgnoreCase) > -1) return true;
                }

                if (EntryMatch == "") return false;  // only attempt to regex match if we have a pattern
                var isMatch = Regex.IsMatch(entry, EntryMatch);
                return isMatch;

            }

            public string GetKeyFromLine(string line)
            {
                return Regex.Match(line, KeyMatch).Value;
            }

            public bool IsStartOfEntry(string line)
            {
                return Regex.IsMatch(line, KeyMatch);
            }
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
                    Console.WriteLine(sortConfig.GetHelpInfoFromAssembly(Assembly.GetExecutingAssembly()));
                    return;
                }

                var logentries = new SortedList<string, string>(new DuplicateKeyComparer<string>());

                string key = "";
                var entry = "";
                var sb = new StringBuilder();

                // SCAN

                // figure out if we are sorting a single file or a folder of files.
                IEnumerable<string> enumerateFiles;
                if (File.Exists(sortConfig.Path))
                {
                    enumerateFiles = new String[] {sortConfig.Path};
                }
                else
                {
                    enumerateFiles=Directory.EnumerateFiles(sortConfig.Path);
                }
                foreach (var file in enumerateFiles)
                {
                    if(file.Equals(sortConfig.OutputFile,StringComparison.InvariantCultureIgnoreCase)) continue; // skip our output file in case it already exists.
                    if(!Regex.IsMatch(file,sortConfig.InputFileMatch)) continue;  // skip files we are ignoring

                    foreach (var line in File.ReadLines(file))
                    {
                        if (sortConfig.IsStartOfEntry(line))
                        {
                            if (key == "") key = sortConfig.GetKeyFromLine(line);

                            entry = sb.ToString();
                            // check if we should add entry.
                            if (sortConfig.ShouldAddEntry(entry)) logentries.Add(key, entry);
                            
                            sb = new StringBuilder();
                            // set new key
                            key = sortConfig.GetKeyFromLine(line);
                            sb.Append(line.Trim());
                            continue;
                        }
                        sb.Append("\n" + sortConfig.EntryPrefix + line.TrimEnd( '\r', '\n' ));
                   }
                    
                    // check if we should add entry.
                    entry = sb.ToString();
                    if(entry.Length>0 && sortConfig.ShouldAddEntry(entry)) logentries.Add(key, entry);
                    sb = new StringBuilder();
                }


                // OUTPUT
                if (sortConfig.OutputFile != "")
                {
                    File.Delete(sortConfig.OutputFile);
                    File.WriteAllLines(sortConfig.OutputFile, logentries.Values);
                }
                else
                {
                    foreach (var logentry in logentries.Values)
                    {
                        Console.WriteLine(logentry);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
