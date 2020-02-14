using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Sgml;

namespace Pull
{
    class Program
    {
        private class Parameters
        {
            public string Url;
            public string Xpath;
            public string Regex;
            public string Replacement;
        }

        static void Main(string[] args)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;

            var parameters = GetArgs(args);

            var html = Curl(parameters.Url);

            var xml = XmlFromHtml(html);

            var textLines = XPathToTextLines(parameters.Xpath, xml);

            var resultLines = ProcessRegex(parameters.Regex, parameters.Replacement, textLines);

            OutputResult(resultLines);

            Console.ForegroundColor = originalColor;
        }

        private static void OutputResult(List<string> lines)
        {
            if (lines.Count == 0)
            {
                Console.WriteLine("The result is empty!");
            }
            else
            {
                foreach (string item in lines)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        Console.WriteLine(item);
                    }
                }
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("------------------------------------------");
                Console.WriteLine($"Found {lines.Count} occurrences.");
            }
        }

        private static List<string> ProcessRegex(string regex, string replacement, List<string> textLines)
        {
            Regex re = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

            var resultLines = new List<string>();
            foreach (var line in textLines)
            {
                var result = re.Replace(line, replacement);
                resultLines.Add(result);
            }
            return resultLines;
        }

        private static Parameters GetArgs(string[] args)
        {
            bool isUserInput = false;

            string url = args.Length > 0 ? args[0] : "";
            if (string.IsNullOrWhiteSpace(url))
            {
                Console.Write("Url: ");
                url = Console.ReadLine();
                isUserInput = true;
            }

            var xpath = args.Length > 1 ? args[1] : "";
            if (string.IsNullOrWhiteSpace(xpath))
            {
                Console.Write("XPath: ");
                xpath = Console.ReadLine();
                isUserInput = true;
            }

            // regex is optional
            var regex = args.Length > 2 ? args[2] : "";
            if (regex == "-i")
            {
                isUserInput = true; // 'interactive' switch
                regex = "";
            }
            if (isUserInput)
            {
                if (string.IsNullOrWhiteSpace(regex))
                {
                    Console.Write("Regex: ");
                    regex = Console.ReadLine();
                }
            }

            // replacement is not optional if regex is given
            var replacement = args.Length > 3 ? args[3] : "";
            if (isUserInput)
            {
                if (string.IsNullOrWhiteSpace(replacement))
                {
                    Console.Write("Replacement: ");
                    replacement = Console.ReadLine();
                }
            }

            return new Parameters { Url = url, Xpath = xpath, Regex = regex, Replacement = replacement };
        }

        private static List<string> XPathToTextLines(string xpath, XmlDocument xml)
        {
            var lines = new List<string>();

            try
            {
                var nodes = xml.SelectNodes(xpath);

                foreach (XmlNode item in nodes)
                {
                    string text = "";
                    if (item.Value != null)
                    {
                        text = item.Value;
                    }
                    else
                    {
                        text = item.InnerText;
                    }
                    text = text.Trim(' ', '\n', '\r', '\0', '\t', '\b');
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(text);
                    }
                }
            }
            catch (System.Xml.XPath.XPathException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid XPath string: \r\n" + e.Message);
            }
            return lines;
        }

        public static string Curl(string url)
        {
            StringBuilder sb = new StringBuilder(1024);
            foreach(var line in CurlLines(url))
            {
                sb.Append(line);
            }
            return sb.ToString();
        }

        public static IEnumerable<string> CurlLines(string url)
        {
            Stream stream = null;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod("GET"), url))
                    {
                        var response = httpClient.SendAsync(request).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            stream = response.Content.ReadAsStreamAsync().Result;
                        }
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error during web request:\r\n" + e.Message);
            }

            if (stream != null)
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    while (!reader.EndOfStream)
                    {
                        yield return reader.ReadLine();
                    }
                }
            }
        }

        public static XmlDocument XmlFromHtml(string html)
        {
            // setup SgmlReader
            SgmlReader sgmlReader = new Sgml.SgmlReader();
            sgmlReader.DocType = "HTML";
            sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
            sgmlReader.CaseFolding = Sgml.CaseFolding.ToLower;
            sgmlReader.InputStream = new StringReader(html);

            // create document
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.XmlResolver = null;
            doc.Load(sgmlReader);
            return doc;
        }

    }
}
