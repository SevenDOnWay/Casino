# TienLen Cloud & Live Infrastructure Stress Tester (Render + MongoDB Atlas)

A lightweight, zero-dependency, native .NET load testing CLI designed to measure real internet performance against your live deployment hosted on **Render** (`https://casino-2odk.onrender.com`) backed by **MongoDB Atlas**.

---

## What It Tests on Live Cloud

1. **Auto-Sharding on MongoDB Atlas**:
   - Spawns tables across multiple channels to saturate the 30-tables/channel threshold.
   - Verifies that `LobbyService` dynamically auto-shards to Channel 2, 3, etc. on live Atlas and recommends the optimal channel.
2. **Real Network Latency & RTT Distribution**:
   - Baseline Ping to Render (measuring RTT & jitter).
   - Real-world p50, p95, p99, and Max latency percentiles over public internet.
3. **Packet Loss & Failure Rate Detection**:
   - Detects network drops, client socket timeouts (408), Cloudflare 502/504 gateways, and Render container saturation.
4. **Server Browser & Matchmaking Concurrency**:
   - High-throughput load on `/api/lobby/channels`, `/api/lobby/sessions`, and `/api/lobby/quick-join`.
5. **Heartbeat Keep-Alives & Auto-Teardown**:
   - Background heartbeats to keep Mongo TTL leases active during the test.
   - Clean automatic teardown deleting all stress tables from MongoDB Atlas after the run.

---

## How to Run Live Stress Tests

By default, the tool targets your live Render server: `https://casino-2odk.onrender.com`.

### 1. Standard Live Test (35 Tables, 15 Virtual Clients, 10s Load)
```bash
dotnet run --project Backend/TienLen.LoadTester -- --tables 35 --concurrency 15 --duration 10
```

### 2. Deep Sharding Stress Test (95 Tables across 4 Channels, 25 Workers, 20s Load)
```bash
dotnet run --project Backend/TienLen.LoadTester -- --tables 95 --concurrency 25 --duration 20
```

### 3. High Concurrency Peak Load (50 Virtual Clients, 30s)
```bash
dotnet run --project Backend/TienLen.LoadTester -- --tables 60 --concurrency 50 --duration 30
```

### 4. Switch to Localhost (If Running Locally)
```bash
dotnet run --project Backend/TienLen.LoadTester -- --local
# or
dotnet run --project Backend/TienLen.LoadTester -- --url http://localhost:5000
```

---

## Command Line Parameters

| Flag | Default | Description |
| :--- | :--- | :--- |
| `--url` | `https://casino-2odk.onrender.com` | Target URL (defaults to live Render cloud) |
| `--local` | `false` | Quick flag to target `http://localhost:5000` |
| `--tables` | `60` | Number of rooms to register and shard on Mongo Atlas |
| `--concurrency` | `20` | Number of concurrent virtual players |
| `--duration` | `15` | Test duration in seconds for server browser load |
| `--timeout` | `10` | Request timeout in seconds before registering packet loss |
| `--no-cleanup` | `false` | Keep registered test tables in MongoDB Atlas |
