use async_nats::Client;
use bytes::Bytes;
use http_body_util::{BodyExt, Full};
use hyper::body::Incoming;
use hyper::service::service_fn;
use hyper::{Request, Response, StatusCode};
use hyper_util::rt::{TokioExecutor, TokioIo};
use tokio::net::TcpListener;

async fn handle(req: Request<Incoming>, nats_client: Client) -> Result<Response<Full<Bytes>>, hyper::Error> {
    if req.method() != hyper::Method::POST {
        return Ok(response(StatusCode::METHOD_NOT_ALLOWED, "POST only"));
    }

    let body = req.into_body().collect().await?.to_bytes();
    // println!("received ingestion payload: {}", String::from_utf8_lossy(&body));

    if let Err(err) = nats_client.publish("errors", body).await {
        return Ok(response(
            StatusCode::BAD_GATEWAY,
            format!("failed to publish message: {err}"),
        ));
    }

    Ok(Response::new(Full::new(Bytes::new())))
}

fn response(status: StatusCode, message: impl Into<Bytes>) -> Response<Full<Bytes>> {
    Response::builder()
        .status(status)
        .body(Full::new(message.into()))
        .unwrap()
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let listener = TcpListener::bind("127.0.0.1:7878").await?;
    println!("HTTP/2 ingestion server on 127.0.0.1:7878");

    let nats_client = async_nats::connect("0.0.0.0:4222").await?;
    println!("Connected with NATS on 0.0.0.0:4222");

    loop {
        let (stream, addr) = listener.accept().await?;
        let io = TokioIo::new(stream);

        let nats_client = nats_client.clone();

        tokio::spawn(async move {
            if let Err(e) = hyper::server::conn::http2::Builder::new(TokioExecutor::default())
                .serve_connection(io, service_fn(move |req| handle(req, nats_client.clone())))
                .await
            {
                eprintln!("[{}] connection error: {}", addr, e);
            }
        });
    }
}
