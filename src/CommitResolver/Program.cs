﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using McMaster.Extensions.CommandLineUtils;

namespace CommitResolver
{
    class Program
    {
        static readonly string _aspNetCoreUrlPrevix = "https://dotnet.myget.org/F/aspnetcore-dev/api/v2/package/Microsoft.AspNetCore.App/";
        static readonly string _netCoreUrlPrevix = "https://dotnetcli.azureedge.net/dotnet/Runtime/{0}/dotnet-runtime-{0}-win-x64.zip";
        static readonly HttpClient _httpClient = new HttpClient();

        static int Main(string[] args)
        {
            var app = new CommandLineApplication();

            app.HelpOption();
            var aspNetVersion = app.Option("-a|--aspnet <VERSION>", "The ASP.NET Core versions", CommandOptionType.MultipleValue);
            var runtimeVersion = app.Option("-r|--runtime <VERSION>", "The .NET Core Runtime versions", CommandOptionType.MultipleValue);

            app.OnExecute(() =>
            {
                Task.Run(async () =>
                {
                    if (aspNetVersion.HasValue() && runtimeVersion.HasValue())
                    {
                        Console.WriteLine("Either -a|--aspnet or -r|--runtime parameters is required");
                        return;
                    }

                    if (aspNetVersion.HasValue())
                    {
                        Console.WriteLine("Microsoft.AspNetCore.App");

                        var allValues = new List<string>();

                        foreach(var x in aspNetVersion.Values)
                        {
                            allValues.Add(await GetAspNetCoreCommitHash(x));
                        }

                        if (allValues.Count == 1)
                        {
                            Console.WriteLine($"https://github.com/aspnet/AspNetCore/commit/{allValues[0]}");
                        }
                        else
                        {
                            for (var i = 1; i < allValues.Count; i++)
                            {
                                Console.WriteLine($"https://github.com/aspnet/AspNetCore/compare/{allValues[i-1]}...{allValues[i]}");
                            }
                        }

                    }

                    if (runtimeVersion.HasValue())
                    {
                        var coreClrValues = new List<string>();
                        var coreFxValues = new List<string>();

                        foreach (var x in runtimeVersion.Values)
                        {
                            coreClrValues.Add(await GetRuntimeAssemblyCommitHash(x, "SOS.NETCore.dll"));
                            coreFxValues.Add(await GetRuntimeAssemblyCommitHash(x, "System.Collections.dll"));
                        }

                        if (coreClrValues.Count == 1)
                        {
                            Console.WriteLine("Microsoft.NetCore.App / Core FX");
                            Console.WriteLine($"https://github.com/dotnet/corefx/commit/{coreFxValues[0]}");
                            Console.WriteLine();
                            Console.WriteLine("Microsoft.NetCore.App / Core CLR");
                            Console.WriteLine($"https://github.com/dotnet/coreclr/commit/{coreClrValues[0]}");
                        }
                        else 
                        {
                            for (var i = 1; i < coreClrValues.Count; i++)
                            {
                                Console.WriteLine("Microsoft.NetCore.App / Core FX");
                                Console.WriteLine($"https://github.com/dotnet/corefx/compare/{coreFxValues[i-1]}...{coreFxValues[i]}");
                                Console.WriteLine();
                                Console.WriteLine("Microsoft.NetCore.App / Core CLR");
                                Console.WriteLine($"https://github.com/dotnet/coreclr/compare/{coreClrValues[i-1]}...{coreClrValues[i]}");
                            }
                        }
                    }

                }).GetAwaiter().GetResult();

            });

            return app.Execute(args);
        }

        private static async Task<string> GetAspNetCoreCommitHash(string aspNetCoreVersion)
        {
            var packagePath = Path.GetTempFileName();

            try
            {
                // Download Microsoft.AspNet.App

                var aspNetAppUrl = _aspNetCoreUrlPrevix + aspNetCoreVersion;
                if (!await DownloadFileAsync(aspNetAppUrl, packagePath))
                {
                    return null;
                }

                // Extract the .nuspec file

                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var aspNetCoreNuSpecPath = Path.GetTempFileName();

                    try
                    {
                        var entry = archive.GetEntry("Microsoft.AspNetCore.App.nuspec");
                        entry.ExtractToFile(aspNetCoreNuSpecPath, true);

                        var root = XDocument.Parse(await File.ReadAllTextAsync(aspNetCoreNuSpecPath)).Root;

                        XNamespace xmlns = root.Attribute("xmlns").Value;
                        return root
                            .Element(xmlns + "metadata")
                            .Element(xmlns + "repository")
                            .Attribute("commit").Value;
                    }
                    finally
                    {
                        try
                        {
                            File.Delete(aspNetCoreNuSpecPath);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    File.Delete(packagePath);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: Failed to delete file {packagePath}");
                    Console.WriteLine(e);
                }
            }
        }

        private static async Task<string> GetRuntimeAssemblyCommitHash(string netCoreAppVersion, string assemblyName)
        {
            var packagePath = Path.GetTempFileName();

            try
            {
                // Download the runtime

                var netCoreAppUrl = String.Format(_netCoreUrlPrevix, netCoreAppVersion);
                if (!await DownloadFileAsync(netCoreAppUrl, packagePath))
                {
                    return null;
                }

                // Extract the .nuspec file

                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var versionAssemblyPath = Path.GetTempFileName();

                    try
                    {
                        var entry = archive.GetEntry($@"shared\Microsoft.NETCore.App\{netCoreAppVersion}\{assemblyName}");
                        entry.ExtractToFile(versionAssemblyPath, true);

                        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(versionAssemblyPath))
                        {
                            var informationalVersionAttribute = assembly.CustomAttributes.Where(x => x.AttributeType.Name == "AssemblyInformationalVersionAttribute").FirstOrDefault();
                            var argumentValule = informationalVersionAttribute.ConstructorArguments[0].Value.ToString();

                            var commitHashRegex = new Regex("[0-9a-f]{40}");

                            var match = commitHashRegex.Match(argumentValule);

                            if (match.Success)
                            {
                                return match.Value;
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            File.Delete(versionAssemblyPath);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    File.Delete(packagePath);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: Failed to delete file {packagePath}");
                    Console.WriteLine(e);
                }
            }
        }

        private static async Task<bool> DownloadFileAsync(string url, string outputPath, int maxRetries = 3, int timeout = 5)
        {
            for (var i = 0; i < maxRetries; ++i)
            {
                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token);
                    response.EnsureSuccessStatusCode();

                    // This probably won't use async IO on windows since the stream
                    // needs to created with the right flags
                    using (var stream = File.Create(outputPath))
                    {
                        // Copy the response stream directly to the file stream
                        await response.Content.CopyToAsync(stream);
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error while downloading {url}:");
                    Console.WriteLine(e);
                }
            }

            return false;
        }
    }
}
