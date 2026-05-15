# App Analytics

This repository contains a small log pipeline built around HTTP ingestion, NATS, and ClickHouse.

## Purpose

The system captures HTTP log batches from a .NET application, forwards them through NATS, and stores them in ClickHouse for later analysis.

## Architecture

1. `instrumentation/` and `dotnet-sdk/`
   - .NET instrumentation and logging helpers.
   - Produce JSON batches of log entries.
2. `ingestion-api/`
   - Rust HTTP service that receives the batch payload.
   - Logs the received body.
   - Publishes the raw JSON to the NATS `errors` subject.
3. `processor/`
   - Rust background service that subscribes to `errors`.
   - Parses each JSON batch.
   - Inserts the rows into ClickHouse using the native protocol.
   - Stores a `requestId` so inbound and outbound events from the same request can be joined.

## Data Flow

`dotnet-sdk` / `instrumentation` -> `ingestion-api` -> NATS `errors` subject -> `processor` -> ClickHouse

Each log entry carries a `requestId` that is assigned once per ASP.NET request and reused by both inbound and outbound logs.

## Local Services

- NATS: `127.0.0.1:4222`
- ClickHouse native protocol: `127.0.0.1:9000`

## ClickHouse Migration

If the `logs` table already exists, run `scripts/add_request_id_to_clickhouse.sql` once to add the `request_id` column.
