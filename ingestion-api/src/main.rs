use bytes::Bytes;
use http_body_util::{BodyExt, Full};
use hyper::body::Incoming;
use hyper::service::service_fn;
use hyper::{Request, Response, StatusCode};
use hyper_util::rt::{TokioExecutor, TokioIo};
use serde::{Deserialize, Serialize};
use std::sync::OnceLock;
use tokio::fs::OpenOptions;
use tokio::io::AsyncWriteExt;
use tokio::net::TcpListener;
use tokio::sync::Mutex;

const LOG_FILE: &str = "failed-requests.log";

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct LogEntry {
    timestamp: String,
    direction: String,
    method: String,
    url: String,
    status_code: i32,
    elapsed_ms: f64,
    request: Option<String>,
    response: Option<String>,
    error: Option<String>,
}

async fn handle(req: Request<Incoming>) -> Result<Response<Full<Bytes>>, hyper::Error> {
    if req.method() != hyper::Method::POST {
        return Ok(response(StatusCode::METHOD_NOT_ALLOWED, "POST only"));
    }

    let body = req.into_body().collect().await?.to_bytes();
    let entries: Vec<LogEntry> = match serde_json::from_slice(&body) {
        Ok(entries) => entries,
        Err(err) => {
            return Ok(response(
                StatusCode::BAD_REQUEST,
                format!("invalid batch: {err}"),
            ));
        }
    };

    if let Err(err) = append_batch_to_log(&entries).await {
        eprintln!("failed to append log batch: {err}");
    }

    Ok(Response::new(Full::new(Bytes::new())))
}

async fn append_batch_to_log(entries: &[LogEntry]) -> std::io::Result<()> {
    let _guard = log_lock().lock().await;

    let mut file = OpenOptions::new()
        .create(true)
        .append(true)
        .open(LOG_FILE)
        .await?;

    file.write_all(format!("received batch: {} entries\n", entries.len()).as_bytes())
        .await?;

    let batch = serde_json::to_string_pretty(entries)
        .unwrap_or_else(|err| format!("{{\"error\":\"failed to serialize batch: {err}\"}}"));
    file.write_all(batch.as_bytes()).await?;
    file.write_all(b"\n\n").await?;
    file.flush().await?;

    Ok(())
}

fn log_lock() -> &'static Mutex<()> {
    static LOCK: OnceLock<Mutex<()>> = OnceLock::new();
    LOCK.get_or_init(|| Mutex::new(()))
}

fn response(status: StatusCode, message: impl Into<Bytes>) -> Response<Full<Bytes>> {
    Response::builder()
        .status(status)
        .body(Full::new(message.into()))
        .unwrap()
}

#[tokio::main]
async fn main() -> std::io::Result<()> {
    let listener = TcpListener::bind("127.0.0.1:7878").await?;
    println!("HTTP/2 ingestion server on 127.0.0.1:7878");

    loop {
        let (stream, addr) = listener.accept().await?;
        let io = TokioIo::new(stream);

        tokio::spawn(async move {
            if let Err(e) = hyper::server::conn::http2::Builder::new(TokioExecutor::default())
                .serve_connection(io, service_fn(handle))
                .await
            {
                eprintln!("[{}] connection error: {}", addr, e);
            }
        });
    }
}
