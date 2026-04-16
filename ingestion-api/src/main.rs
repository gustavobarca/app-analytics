use bytes::Bytes;
use http_body_util::Full;
use hyper::{Request, Response, body::Incoming};
use hyper::service::service_fn;
use hyper_util::rt::{TokioExecutor, TokioIo};
use hyper_util::server::conn::auto;
use tokio::net::TcpListener;

async fn handle(req: Request<Incoming>) -> Result<Response<Full<Bytes>>, hyper::Error> {
    println!("{} {} {:?}", req.method(), req.uri(), req.version());
    for (name, value) in req.headers() {
        println!("  {}: {}", name, value.to_str().unwrap_or("<binary>"));
    }
    Ok(Response::new(Full::new(Bytes::new())))
}

#[tokio::main]
async fn main() -> std::io::Result<()> {
    let listener = TcpListener::bind("127.0.0.1:7878").await?;
    println!("HTTP/2 server on 127.0.0.1:7878");

    loop {
        let (stream, addr) = listener.accept().await?;

        let io = TokioIo::new(stream);

        tokio::spawn(async move {
            if let Err(e) = auto::Builder::new(TokioExecutor::default())
                .serve_connection(io, service_fn(handle))
                .await
            {
                eprintln!("[{}] connection error: {}", addr, e);
            }
        });
    }
}
