using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TitaniumAS.Opc.Client.Common;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;
using System.Threading;
using System.Configuration;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Net.Http;
using System.Diagnostics;

namespace ConsoleApp5
{
    class Program
    {
        static void Main(string[] args)
        {
            TitaniumAS.Opc.Client.Bootstrap.Initialize();
            try
            {
                var thread = new Thread(RunApplication);
                thread.SetApartmentState(ApartmentState.MTA);
                thread.Start();
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine("Error: {0}", e.Message);
                var baseDir = Environment.CurrentDirectory.Replace("bin\\Debug", "");
                var errorDir = baseDir + "\\AppData\\error.txt";
                var getFileSizeErrorLog = new FileInfo(errorDir).Length / (1024 * 1024);
                if (getFileSizeErrorLog >= 5)
                {
                    var errorLog = File.ReadAllLines(errorDir);
                    var lastLog = errorLog.Skip((errorLog.Length - 10) < 0 ? 0 : errorLog.Length - 10);
                    //var lastLog = errorLog.Substring((errorLog.Length - 43) < 0 ? 0 : errorLog.Length - 43);
                    File.WriteAllText(errorDir, "Error Log\n" + string.Join("\n", lastLog));
                }
                File.AppendAllText(errorDir, "\n---------------------\n(" + DateTime.Now.ToString("G") + ") - " + e.Message);
                Console.ForegroundColor = ConsoleColor.White;
                Thread.Sleep(1000);
            }

            void RunApplication()
            {
            Start:
                try
                {
                    Console.Clear();

                    var baseDir = Environment.CurrentDirectory.Replace("bin\\Debug", "");
                    JObject o1 = JObject.Parse(File.ReadAllText(baseDir + "\\AppData\\config.json"));

                    //Uri url = UrlBuilder.Build(o1["url"].ToString());
                    string opcServerName = o1["url"].ToString();
                    string hostName = o1["ip"].ToString();
                    string api = o1["outputApi"].ToString();
                    string outputType = o1["outputType"].ToString();
                    var outputDir = !string.IsNullOrEmpty(o1["outputFile"].ToString()) ? o1["outputFile"].ToString() : baseDir + "\\AppData\\output.json";
                    Uri url = UrlBuilder.Build(opcServerName, hostName);
                    var clientHttp = new HttpClient();
                    using (var server = new OpcDaServer(url))
                    {
                        server.Connect();

                        OpcDaGroup group = server.AddGroup("MyGroup");
                        group.IsActive = true;

                        var parameterList = o1["parameter"].ToString().TrimStart('[').TrimEnd(']').Replace("\"", "");
                        var parameterList2 = parameterList.Split(',');


                        OpcDaItemDefinition[] opcDaItems = new OpcDaItemDefinition[parameterList2.Length];

                        for (var i = 0; i < parameterList2.Length; i++)
                        {
                            System.Console.WriteLine(parameterList2[i].Trim());
                            var itemBool = new OpcDaItemDefinition
                            {
                                ItemId = parameterList2[i].Trim(),
                                IsActive = true
                            };
                            opcDaItems[i] = itemBool;
                        }

                        OpcDaItemResult[] results = group.AddItems(opcDaItems);

                        foreach (OpcDaItemResult result in results)
                        {
                            if (result.Error.Failed)
                                Console.WriteLine("Error adding items: {0}", result.Error);
                        }

                        while (true)
                        {
                            Console.Clear();
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            OpcDaItemValue[] values = group.Read(group.Items, OpcDaDataSource.Device);
                            List<string> newDataList = new List<string>();
                            for (var i = 0; i < values.Length; i++)
                            {
                                try
                                {
                                    if (!values[i].Value.GetType().ToString().Contains("[]"))
                                    {
                                        var newData = new
                                        {
                                            Parameter = parameterList2[i].Trim(),
                                            Value = values[i].Value.ToString(),
                                            Quality = values[i].Quality,
                                            Timestamp = values[i].Timestamp.LocalDateTime.ToString()
                                        };

                                        Console.WriteLine("Value {0} is {1}, {2}", parameterList2[i].Trim(), values[i].Value.ToString(), values[i].Timestamp);

                                        newDataList.Add(System.Text.Json.JsonSerializer.Serialize(newData));

                                    }
                                    else
                                    {
                                        var value = System.Text.Json.JsonSerializer.Serialize(values[i].Value);
                                        var newData = new
                                        {
                                            Parameter = parameterList2[i].Trim(),
                                            Value = value.TrimStart('[').TrimEnd(']').Replace("\"", "").Replace(",,", ",").Split(','),
                                            Quality = values[i].Quality,
                                            Timestamp = values[i].Timestamp.LocalDateTime.ToString()
                                        };

                                        Console.WriteLine("Value {0} is {1}, {2}", parameterList2[i].Trim(), value, values[i].Timestamp);

                                        newDataList.Add(System.Text.Json.JsonSerializer.Serialize(newData));

                                    }

                                }
                                catch (Exception e)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    System.Console.WriteLine("Error: {0}", e.Message);
                                    var errorDir = baseDir + "\\AppData\\error.txt";
                                    var getFileSizeErrorLog = new FileInfo(errorDir).Length / (1024 * 1024);
                                    if (getFileSizeErrorLog >= 5)
                                    {
                                        var errorLog = File.ReadAllLines(errorDir);
                                        var lastLog = errorLog.Skip((errorLog.Length - 10) < 0 ? 0 : errorLog.Length - 10);
                                        //var lastLog = errorLog.Substring((errorLog.Length - 43) < 0 ? 0 : errorLog.Length - 43);
                                        File.WriteAllText(errorDir, "Error Log\n" + string.Join("\n", lastLog));
                                    }
                                    File.AppendAllText(errorDir, "\n---------------------\n(" + DateTime.Now.ToString("G") + ") - " + e.Message);
                                    Console.ForegroundColor = ConsoleColor.White;
                                    Thread.Sleep(1000);
                                    goto Start;
                                }
                            }
                            if (outputType == "file" || outputType == "all")
                            {
                                File.WriteAllText(outputDir, "[" + string.Join(",", newDataList).Replace("\\", "").Replace("\"{", "{").Replace("}\"", "}") + "]");
                            }
                            else if(outputType == "api" || outputType == "all")
                            {
                                var data = new StringContent("[" + string.Join(",", newDataList).Replace("\\", "").Replace("\"{", "{").Replace("}\"", "}") + "]", Encoding.UTF8, "application/json");
                                var postData = clientHttp.PostAsync(api, data);
                            }
                            //Console.WriteLine("post status : {0}", postData.Result.IsSuccessStatusCode ? "success" : "failed");
                            stopwatch.Stop();
                            Console.WriteLine("Elapsed Time is {0} ms", stopwatch.ElapsedMilliseconds);
                            Thread.Sleep(1000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine("Error: {0}", ex.Message);
                    var baseDir = Environment.CurrentDirectory.Replace("bin\\Debug", "");
                    var errorDir = baseDir + "\\AppData\\error.txt";
                    var getFileSizeErrorLog = new FileInfo(errorDir).Length / (1024 * 1024);
                    if (getFileSizeErrorLog >= 5)
                    {
                        var errorLog = File.ReadAllLines(errorDir);
                        var lastLog = errorLog.Skip((errorLog.Length - 10) < 0 ? 0 : errorLog.Length - 10);
                        //var lastLog = errorLog.Substring((errorLog.Length - 43) < 0 ? 0 : errorLog.Length - 43);
                        File.WriteAllText(errorDir, "Error Log\n" + string.Join("\n", lastLog));
                    }
                    File.AppendAllText(errorDir, "\n---------------------\n(" + DateTime.Now.ToString("G") + ") - " + ex.Message);
                    Console.ForegroundColor = ConsoleColor.White;
                    Thread.Sleep(1000);
                    goto Start;
                }
            }
        }
    }
}