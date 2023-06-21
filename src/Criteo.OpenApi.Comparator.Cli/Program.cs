// Copyright (c) Criteo Technology. All rights reserved.
// Licensed under the Apache 2.0 License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Criteo.OpenApi.Comparator.Cli
{
    /// <summary>
    /// Entry point for OpenAPI Comparator command line tool
    /// </summary>
    public static class Program
    {
        /// <param name="args">
        /// Must contain --old|-o and --new|-n parameters which are paths to old and new OpenAPI specification
        /// </param>
        public static int Main(string[] args)
        {
            var parserResult = CommandLine.Parser.Default.ParseArguments<Options>(args);

            if (parserResult.Errors.Any())
            {
                return 1;
            }
            
            var options = parserResult.Value;

            var oldFileFound = TryReadFile(options.OldSpec, out var oldOpenApiSpecification);
            var newFileFound = TryReadFile(options.NewSpec, out var newOpenApiSpecification);

            if (!oldFileFound || !newFileFound)
            {
                Console.WriteLine("Exiting.");
                return 1;
            }
            
            var differences = OpenApiComparator.Compare(oldOpenApiSpecification, newOpenApiSpecification);

            DisplayOutput(differences, options.OutputFormat);
            
            return 0;
        }

        private static bool TryReadFile(string path, out string fileContent)
        {
            var isHttp = path.StartsWith("http",StringComparison.OrdinalIgnoreCase);

            if (isHttp)
            {
                try
                {
                    using (var client = CreateHttpClient())
                    {
                        var response = client.GetAsync(path).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            fileContent = response.Content.ReadAsStringAsync().Result;
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"Error downloading file from URL: {path}. Status code: {response.StatusCode}");
                            fileContent = null;
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading file from URL: {path}. {ex.Message}");
                    fileContent = null;
                    return false;
                }
            }
            else
            {
                try
                {
                    fileContent = File.ReadAllText(path);
                    return true;
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"File not found for: {path}.");
                    fileContent = null;
                    return false;
                }
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            return new HttpClient(handler);
        }

        private static void DisplayOutput(IEnumerable<ComparisonMessage> differences, OutputFormat outputFormat)
        {
            if (outputFormat == OutputFormat.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(differences, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            if (outputFormat == OutputFormat.Text)
            {
                Console.WriteLine("\n| **Change Type** | **API** | **Summary** |");
                Console.WriteLine("| ------- | ------- | ------- |");

                var added = differences?.Where(o => o.Code == nameof(ComparisonRules.AddedOperation));
                var changed = differences?.Where(o => o.Mode == MessageType.Update)?.GroupBy( r => r.NewApiDetail)?.Select( g => g.First());
                if (added is not null && added.Any() )
                {
                    added.ToList().ForEach(o => Console.WriteLine($"| Addition | {o.NewApiDetail} |"));
                }
                
                if (changed is not null && changed.Any())
                {
                    changed.ToList().ForEach(o => Console.WriteLine($"| Update | {o.NewApiDetail} |"));
                }                
            }
        }
    }
}
