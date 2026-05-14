use futures_util::StreamExt;
use klickhouse::ClientOptions;
use serde::{Deserialize, Serialize};

const NATS_URL: &str = "127.0.0.1:4222";
const CLICKHOUSE_ADDR: &str = "127.0.0.1:9000";

#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct IncomingLogEntry {
    timestamp: String,
    direction: String,
    method: String,
    url: String,
    status_code: i32,
    elapsed_ms: f64,
    response: Option<String>,
    error: Option<String>,
}

#[derive(Debug, Clone, klickhouse::Row)]
struct DbLogEntry {
    timestamp: String,
    direction: String,
    method: String,
    url: String,
    status_code: i32,
    elapsed_ms: f64,
    response: Option<String>,
    error: Option<String>,
}

impl From<IncomingLogEntry> for DbLogEntry {
    fn from(entry: IncomingLogEntry) -> Self {
        Self {
            timestamp: entry.timestamp,
            direction: entry.direction,
            method: entry.method,
            url: entry.url,
            status_code: entry.status_code,
            elapsed_ms: entry.elapsed_ms,
            response: entry.response,
            error: entry.error,
        }
    }
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let nats_client = async_nats::connect(NATS_URL).await?;
    println!("Connected with NATS on {NATS_URL}");

    let clickhouse = klickhouse::Client::connect(CLICKHOUSE_ADDR, ClientOptions::default()).await?;
    println!("Connected with ClickHouse on {CLICKHOUSE_ADDR}");

    let mut messages = nats_client.subscribe("errors").await?;
    println!("Listening on subject: errors");

    while let Some(message) = messages.next().await {
        if let Err(err) = handle_message(&clickhouse, &message.payload).await {
            eprintln!("failed to process batch: {err}");
        }
    }

    Ok(())
}

async fn handle_message(clickhouse: &klickhouse::Client, payload: &[u8]) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let batch: Vec<IncomingLogEntry> = serde_json::from_slice(payload)?;

    if batch.is_empty() {
        return Ok(());
    }

    let rows: Vec<DbLogEntry> = batch.into_iter().map(Into::into).collect();
    let num_rows = rows.len().to_string();
    println!("rows inserted: {num_rows}");

    clickhouse
        .insert_native_block("INSERT INTO logs FORMAT native", rows)
        .await?;

    println!("saved batch to ClickHouse");
    Ok(())
}