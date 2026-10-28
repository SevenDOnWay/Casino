using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TienLen.Server.DTOs;

namespace TienLen.LoadTester {
    public class Program {
        private const string LiveRenderUrl = "https://casino-2odk.onrender.com";

        private static readonly JsonSerializerOptions JsonOptions = new() {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<int> Main( string[] args ) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("     TienLen Cloud & Live Infrastructure Stress Tester (Render + Mongo Atlas)  ");
            Console.WriteLine("===============================================================================\n");

            // 1. Target resolution
            bool useLocal = args.Contains("--local");
            string defaultUrl = useLocal ? "http://localhost:5000" : LiveRenderUrl;
            string baseUrl = GetArg(args, "--url", defaultUrl).TrimEnd('/');

            int betStake = int.Parse(GetArg(args, "--stake", "5"));
            int tablesToSpawn = int.Parse(GetArg(args, "--tables", "60")); // 2 full channels (30 per channel)
            int concurrency = int.Parse(GetArg(args, "--concurrency", "20"));
            int durationSeconds = int.Parse(GetArg(args, "--duration", "15"));
            int timeoutSeconds = int.Parse(GetArg(args, "--timeout", "10"));
            bool skipCleanup = args.Contains("--no-cleanup");

            Console.WriteLine($"[Target] Remote Host     : {baseUrl} {(baseUrl == LiveRenderUrl ? "(LIVE RENDER CLOUD)" : "")}");
            Console.WriteLine($"[Target] Database        : MongoDB Atlas (via Render Backend)");
            Console.WriteLine($"[Config] Bet Stake       : {betStake}");
            Console.WriteLine($"[Config] Shard Tables    : {tablesToSpawn} tables (testing capacity & channel auto-sharding)");
            Console.WriteLine($"[Config] Concurrency     : {concurrency} parallel virtual clients");
            Console.WriteLine($"[Config] Test Duration   : {durationSeconds} seconds");
            Console.WriteLine($"[Config] Request Timeout : {timeoutSeconds}s");
            Console.WriteLine($"[Config] Auto Cleanup    : {(!skipCleanup ? "Yes (removes test tables after run)" : "No")}\n");

            using var handler = new SocketsHttpHandler {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 200,
                EnableMultipleHttp2Connections = true
            };
            using var httpClient = new HttpClient(handler) {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

            // ===============================================================
            // BASELINE PING & NETWORK RTT MEASUREMENT
            // ===============================================================
            Console.WriteLine("--- [Step 1: Baseline Network Latency & RTT Check] ---");
            var baselineLatencies = new List<double>();
            for ( int i = 1; i <= 5; i++ ) {
                var pSw = Stopwatch.StartNew();
                try {
                    var healthResp = await httpClient.GetAsync("/health");
                    pSw.Stop();
                    if ( healthResp.IsSuccessStatusCode ) {
                        baselineLatencies.Add(pSw.Elapsed.TotalMilliseconds);
                        Console.WriteLine($"  Ping {i}/5 -> {pSw.Elapsed.TotalMilliseconds:F1} ms (Status: {(int)healthResp.StatusCode} OK)");
                    }
                    else {
                        Console.WriteLine($"  Ping {i}/5 -> Received {(int)healthResp.StatusCode} in {pSw.Elapsed.TotalMilliseconds:F1} ms");
                    }
                }
                catch ( Exception ex ) {
                    pSw.Stop();
                    Console.WriteLine($"  Ping {i}/5 -> FAILED ({ex.GetType().Name}: {ex.Message}) in {pSw.Elapsed.TotalMilliseconds:F1} ms");
                }
                await Task.Delay(200);
            }

            if ( baselineLatencies.Count == 0 ) {
                Console.WriteLine("\n[CRITICAL ERROR] Unable to establish baseline connection with target host.");
                Console.WriteLine("Possible causes: Render instance is spun down/cold starting, DNS failure, or incorrect URL.");
                return 1;
            }

            double baseMin = baselineLatencies.Min();
            double baseAvg = baselineLatencies.Average();
            double baseMax = baselineLatencies.Max();
            double jitter = baseMax - baseMin;
            Console.WriteLine($"Baseline RTT: Avg={baseAvg:F1} ms | Min={baseMin:F1} ms | Max={baseMax:F1} ms | Jitter={jitter:F1} ms\n");

            var registeredSessions = new ConcurrentBag<string>();
            var metrics = new ConcurrentBag<RequestMetric>();

            // ===============================================================
            // STEP 2: LIVE SHARDING STRESS TEST (MongoDB Atlas Writes via Render)
            // ===============================================================
            Console.WriteLine($"--- [Step 2: Live Sharding Write Stress ({tablesToSpawn} Tables)] ---");
            Console.WriteLine($"Registering tables with Concurrency={concurrency} across channels (max 30 tables/channel)...");
            var phase2Sw = Stopwatch.StartNew();

            var spawnTasks = Enumerable.Range(1, tablesToSpawn).Select(i => {
                int ch = ((i - 1) / 30) + 1;
                string room = $"LIVE_STRESS_{betStake}_{ch}_{i:D3}_{Guid.NewGuid().ToString("N")[..4]}";
                return (Index: i, Channel: ch, Room: room);
            }).ToList();

            await Parallel.ForEachAsync(spawnTasks, new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async ( task, ct ) => {
                var sw = Stopwatch.StartNew();
                bool success = false;
                int statusCode = 0;
                string errorType = "";

                try {
                    var req = new RegisterSessionRequest(
                        SessionName: task.Room,
                        BetStake: betStake,
                        Channel: task.Channel,
                        PlayerCount: 1,
                        MaxPlayers: 4
                    );

                    var resp = await httpClient.PostAsJsonAsync("/api/lobby/register", req, ct);
                    statusCode = (int)resp.StatusCode;
                    success = resp.IsSuccessStatusCode;
                    if ( success ) {
                        registeredSessions.Add(task.Room);
                    }
                    else {
                        errorType = $"HTTP_{statusCode}";
                    }
                }
                catch ( TaskCanceledException ) {
                    statusCode = 408;
                    errorType = "TIMEOUT_PACKET_LOSS";
                }
                catch ( HttpRequestException ex ) {
                    statusCode = 0;
                    errorType = $"NET_DROP: {ex.StatusCode?.ToString() ?? ex.Message}";
                }
                catch ( Exception ex ) {
                    statusCode = 0;
                    errorType = $"ERROR: {ex.GetType().Name}";
                }
                finally {
                    sw.Stop();
                    metrics.Add(new RequestMetric("POST /api/lobby/register", sw.Elapsed.TotalMilliseconds, success, statusCode, errorType));
                }
            });

            phase2Sw.Stop();
            Console.WriteLine($"[Step 2 Complete] {registeredSessions.Count}/{tablesToSpawn} tables registered in {phase2Sw.Elapsed.TotalSeconds:F2}s.");

            // Verify live sharding state on Mongo Atlas
            Console.WriteLine("\n[Checking Live MongoDB Atlas Sharding via API]");
            var chQuerySw = Stopwatch.StartNew();
            var chResp = await httpClient.GetAsync($"/api/lobby/channels?betStake={betStake}");
            chQuerySw.Stop();

            if ( chResp.IsSuccessStatusCode ) {
                var body = await chResp.Content.ReadFromJsonAsync<ApiResponse<ChannelQueryResponse>>(JsonOptions);
                if ( body?.Data != null ) {
                    var data = body.Data;
                    Console.WriteLine($"  - Query Latency: {chQuerySw.Elapsed.TotalMilliseconds:F1} ms");
                    Console.WriteLine($"  - Active Channels on Render/Atlas: [{string.Join(", ", data.Channels)}]");
                    Console.WriteLine($"  - Recommended Channel: {data.RecommendedChannel}");

                    foreach ( var ch in data.Channels ) {
                        var sessResp = await httpClient.GetAsync($"/api/lobby/sessions?betStake={betStake}&channel={ch}");
                        if ( sessResp.IsSuccessStatusCode ) {
                            var sessData = await sessResp.Content.ReadFromJsonAsync<ApiResponse<List<LobbySessionDto>>>(JsonOptions);
                            int count = sessData?.Data?.Count ?? 0;
                            string status = count >= 30 ? "FULL (30/30 tables -> Auto-Sharded)" : $"{count}/30 capacity";
                            Console.WriteLine($"    * Channel {ch}: {status}");
                        }
                    }
                }
            }
            else {
                Console.WriteLine($"  - Failed to query channels: HTTP {(int)chResp.StatusCode}");
            }

            // ===============================================================
            // STEP 3: HIGH CONCURRENCY SERVER BROWSER & MATCHMAKING LOAD
            // ===============================================================
            Console.WriteLine($"\n--- [Step 3: High-Concurrency Live Traffic ({durationSeconds}s @ {concurrency} Workers)] ---");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
            var loadMetrics = new ConcurrentBag<RequestMetric>();

            // Heartbeat Keep-Alive Loop in Background
            var heartbeatTask = Task.Run(async () => {
                while ( !cts.Token.IsCancellationRequested ) {
                    await Parallel.ForEachAsync(registeredSessions, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async ( sessionName, ct ) => {
                        try {
                            var hbReq = new HeartbeatRequest(sessionName, 2);
                            var resp = await httpClient.PostAsJsonAsync("/api/lobby/heartbeat", hbReq, ct);
                            loadMetrics.Add(new RequestMetric("POST /api/lobby/heartbeat", 0, resp.IsSuccessStatusCode, (int)resp.StatusCode, resp.IsSuccessStatusCode ? "" : "HB_FAIL"));
                        }
                        catch { }
                    });
                    await Task.Delay(4000, cts.Token).ContinueWith(_ => { });
                }
            });

            // Virtual User Workers
            var workerTasks = Enumerable.Range(1, concurrency).Select(async workerId => {
                var rand = new Random(workerId * 37);
                while ( !cts.Token.IsCancellationRequested ) {
                    int roll = rand.Next(100);
                    var sw = Stopwatch.StartNew();
                    bool success = false;
                    int statusCode = 0;
                    string endpoint = "";
                    string errorType = "";

                    bool wasCancelledByTestStop = false;
                    try {
                        if ( roll < 45 ) {
                            // 45%: Browse table sessions in random channel
                            int ch = rand.Next(1, 4);
                            endpoint = "GET /api/lobby/sessions";
                            var resp = await httpClient.GetAsync($"/api/lobby/sessions?betStake={betStake}&channel={ch}", cts.Token);
                            statusCode = (int)resp.StatusCode;
                            success = resp.IsSuccessStatusCode;
                            if ( !success ) errorType = $"HTTP_{statusCode}";
                        }
                        else if ( roll < 75 ) {
                            // 30%: Query available channels & recommendation
                            endpoint = "GET /api/lobby/channels";
                            var resp = await httpClient.GetAsync($"/api/lobby/channels?betStake={betStake}", cts.Token);
                            statusCode = (int)resp.StatusCode;
                            success = resp.IsSuccessStatusCode;
                            if ( !success ) errorType = $"HTTP_{statusCode}";
                        }
                        else {
                            // 25%: Quick Join Matchmaking
                            endpoint = "GET /api/lobby/quick-join";
                            var resp = await httpClient.GetAsync($"/api/lobby/quick-join?betStake={betStake}", cts.Token);
                            statusCode = (int)resp.StatusCode;
                            success = resp.IsSuccessStatusCode;
                            if ( !success ) errorType = $"HTTP_{statusCode}";
                        }
                    }
                    catch ( TaskCanceledException ) when ( !cts.Token.IsCancellationRequested ) {
                        statusCode = 408;
                        errorType = "TIMEOUT_PACKET_LOSS";
                    }
                    catch ( OperationCanceledException ) {
                        wasCancelledByTestStop = true;
                        break;
                    }
                    catch ( HttpRequestException ex ) {
                        statusCode = 0;
                        errorType = $"NET_DROP: {ex.StatusCode?.ToString() ?? ex.Message}";
                    }
                    catch ( Exception ex ) {
                        statusCode = 0;
                        errorType = $"ERR: {ex.GetType().Name}";
                    }
                    finally {
                        sw.Stop();
                        if ( !wasCancelledByTestStop ) {
                            loadMetrics.Add(new RequestMetric(endpoint, sw.Elapsed.TotalMilliseconds, success, statusCode, errorType));
                        }
                    }

                    // Human-like jitter delay between requests (15ms - 50ms)
                    await Task.Delay(rand.Next(15, 50), cts.Token).ContinueWith(_ => { });
                }
            }).ToList();

            await Task.WhenAll(workerTasks);
            await heartbeatTask;

            // ===============================================================
            // STEP 4: CLEANUP & TEARDOWN
            // ===============================================================
            if ( !skipCleanup && registeredSessions.Count > 0 ) {
                Console.WriteLine("\n--- [Step 4: Cleanup & Room Teardown on Mongo Atlas] ---");
                int cleaned = 0;
                await Parallel.ForEachAsync(registeredSessions, new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async ( room, ct ) => {
                    try {
                        var resp = await httpClient.DeleteAsync($"/api/lobby/sessions/{room}", ct);
                        if ( resp.IsSuccessStatusCode ) Interlocked.Increment(ref cleaned);
                    }
                    catch { }
                });
                Console.WriteLine($"[Teardown Complete] Successfully cleaned up {cleaned}/{registeredSessions.Count} rooms on remote Mongo Atlas.");
            }

            // ===============================================================
            // TELEMETRY & NETWORK LOSS REPORT
            // ===============================================================
            PrintDetailedTelemetry("STEP 2: LIVE ATLAS SHARDING (WRITES)", metrics);
            PrintDetailedTelemetry("STEP 3: SERVER BROWSER & MATCHMAKING (READS)", loadMetrics.Where(m => m.Endpoint != "POST /api/lobby/heartbeat"));

            // Overall Network Summary
            PrintNetworkLossSummary(baseUrl, baseAvg, metrics.Concat(loadMetrics));

            return 0;
        }

        private static void PrintDetailedTelemetry( string sectionTitle, IEnumerable<RequestMetric> list ) {
            var valid = list.Where(m => m.DurationMs > 0).ToList();
            if ( valid.Count == 0 ) return;

            Console.WriteLine($"\n===============================================================================");
            Console.WriteLine($" TELEMETRY: {sectionTitle}");
            Console.WriteLine($"===============================================================================");
            Console.WriteLine($"{"Endpoint",-28} | {"Sent",-6} | {"Fail",-5} | {"Loss%",-6} | {"Avg(ms)",-8} | {"p50(ms)",-8} | {"p95(ms)",-8} | {"p99(ms)",-8} | {"Max(ms)",-8}");
            Console.WriteLine(new string('-', 102));

            foreach ( var g in valid.GroupBy(m => m.Endpoint) ) {
                var sorted = g.Select(m => m.DurationMs).OrderBy(d => d).ToList();
                int total = sorted.Count;
                int fails = g.Count(m => !m.Success);
                double lossPct = (fails / (double)total) * 100.0;
                double avg = sorted.Average();
                double p50 = GetPercentile(sorted, 50);
                double p95 = GetPercentile(sorted, 95);
                double p99 = GetPercentile(sorted, 99);
                double max = sorted.Max();

                Console.WriteLine($"{g.Key,-28} | {total,-6} | {fails,-5} | {lossPct,5:F1}% | {avg,-8:F1} | {p50,-8:F1} | {p95,-8:F1} | {p99,-8:F1} | {max,-8:F1}");
            }
        }

        private static void PrintNetworkLossSummary( string targetUrl, double baseLatency, IEnumerable<RequestMetric> allMetrics ) {
            var metricsList = allMetrics.Where(m => m.DurationMs > 0).ToList();
            int total = metricsList.Count;
            if ( total == 0 ) return;

            int successes = metricsList.Count(m => m.Success);
            int timeouts = metricsList.Count(m => m.ErrorType.Contains("TIMEOUT") || m.StatusCode == 408);
            int netDrops = metricsList.Count(m => m.ErrorType.Contains("NET_DROP"));
            int serverErrors = metricsList.Count(m => m.StatusCode >= 500);
            int clientErrors = metricsList.Count(m => m.StatusCode >= 400 && m.StatusCode < 500 && m.StatusCode != 408);
            int totalFailed = total - successes;

            double lossRate = ((timeouts + netDrops) / (double)total) * 100.0;
            double failureRate = (totalFailed / (double)total) * 100.0;
            double throughputRps = total / Math.Max(1.0, metricsList.Max(m => m.DurationMs) / 1000.0);

            Console.WriteLine("\n===============================================================================");
            Console.WriteLine("                   LIVE CLOUD INFRASTRUCTURE HEALTH SUMMARY                   ");
            Console.WriteLine("===============================================================================");
            Console.WriteLine($"Target Host          : {targetUrl}");
            Console.WriteLine($"Baseline Ping (RTT)  : {baseLatency:F1} ms");
            Console.WriteLine($"Total Requests Sent  : {total}");
            Console.WriteLine($"Successful (2xx)     : {successes} ({(successes / (double)total) * 100.0:F1}%)");
            Console.WriteLine($"Total Failed         : {totalFailed} ({failureRate:F1}%)");
            Console.WriteLine($"  - Network Drops    : {netDrops}");
            Console.WriteLine($"  - Request Timeouts : {timeouts}");
            Console.WriteLine($"  - Server 5xx / 502 : {serverErrors} (Render gateway or server overload)");
            Console.WriteLine($"  - Client 4xx       : {clientErrors}");
            Console.WriteLine($"Packet / Drop Loss % : {lossRate:F2}%");
            Console.WriteLine("===============================================================================\n");
        }

        private static double GetPercentile( List<double> sorted, int percentile ) {
            if ( sorted.Count == 0 ) return 0;
            int idx = (int)Math.Ceiling((percentile / 100.0) * sorted.Count) - 1;
            return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
        }

        private static string GetArg( string[] args, string flag, string defaultValue ) {
            int idx = Array.IndexOf(args, flag);
            if ( idx >= 0 && idx < args.Length - 1 ) {
                return args[idx + 1];
            }
            return defaultValue;
        }
    }

    public record RequestMetric(string Endpoint, double DurationMs, bool Success, int StatusCode, string ErrorType);
}
