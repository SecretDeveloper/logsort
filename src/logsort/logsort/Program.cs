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

        internal static string BreakStringToLength(string line, int maximumLineLength)
        {
            if (string.IsNullOrEmpty(line)) return "";
            if (maximumLineLength <= 1) throw new ArgumentOutOfRangeException("maximumLineLength");
            if (line.Length <= maximumLineLength - 1) return line;

            var maxLineLength = maximumLineLength;

            var sb = new StringBuilder();
            var startingWhiteSpace = GetLeadingWhitespaceAsSpaces(line);
            var startingWhiteSpaceLength = startingWhiteSpace.Length;

            var currentIndex = 0;
            var possibleIndex = 0;

            var keepGoing = true;
            while (keepGoing)
            {
                var scanIndex = line.IndexOf(' ', possibleIndex + 1);
                if (scanIndex != -1) scanIndex += 1;  // move to location after the space so we wrap at start of word.

                if (scanIndex - currentIndex + startingWhiteSpaceLength > maxLineLength)
                {
                    sb.Append(line.Substring(currentIndex, possibleIndex - currentIndex));
                    sb.AppendLine();
                    sb.Append(startingWhiteSpace);
                    currentIndex = possibleIndex;
                }
                // no more spaces
                if (scanIndex == -1)
                {
                    var lengthRemaining = line.Length - currentIndex;
                    if (currentIndex == 0)
                    {
                        if (lengthRemaining > maxLineLength)
                        {
                            sb.AppendLine(line.Substring(currentIndex, maxLineLength));
                            sb.Append(startingWhiteSpace);
                            currentIndex += maxLineLength;
                        }
                        else
                        {
                            sb.Append(line.Substring(currentIndex, lengthRemaining));
                            keepGoing = false;
                        }
                    }
                    else
                    {
                        if (lengthRemaining + startingWhiteSpaceLength > maxLineLength)
                        {
                            sb.AppendLine(line.Substring(currentIndex, maxLineLength - startingWhiteSpaceLength));
                            sb.Append(startingWhiteSpace);
                            currentIndex += maxLineLength - startingWhiteSpaceLength;
                        }
                        else
                        {
                            sb.Append(line.Substring(currentIndex, lengthRemaining));
                            keepGoing = false;
                        }
                    }
                }
                else
                {
                    possibleIndex = scanIndex;
                }
            }

            return sb.ToString();
        }

        private static string GetLeadingWhitespaceAsSpaces(string line)
        {
            int count = 0;
            foreach (var c in line)
            {
                if (!Char.IsWhiteSpace(c)) break;
                if (c == ' ') count++;
                if (c.Equals('\t')) count += 4;
            }
            return new string(' ', count);
        }
    }
}
