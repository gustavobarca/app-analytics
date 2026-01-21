use tokio::{net::TcpListener, time::{Duration, sleep}};

// fn main() {
//     let listener = TcpListener::bind("127.0.0.1:7878").unwrap();

//     for stream in listener.incoming() {
//         let stream = stream.unwrap();

//         println!("handling one client...");
//         thread::sleep(Duration::from_secs(5));
//         println!("done");
//     }
// }


#[tokio::main]
async fn main() -> std::io::Result<()> {
    let listener = TcpListener::bind("127.0.0.1:7878").await?;
    println!("async server on 127.0.0.1:7878");

    loop {
        let (_stream, _addr) = listener.accept().await?;

        tokio::spawn(async move {
            println!("handling one client...");
            sleep(Duration::from_secs(5)).await;
            println!("done");
        });
    }
}


// #[tokio::main]
// async fn main() {
//     let t1 = tokio::spawn(async move {
//         println!("handling one client...");
//         sleep(Duration::from_secs(5)).await;
//         println!("done");
//     });

//     let t2 = tokio::spawn(async move {
//         println!("handling two client...");
//         sleep(Duration::from_secs(5)).await;
//         println!("done");
//     });


//     let _ = tokio::join!(t1, t2);
// }


// fn handle_connection(mut stream: TcpStream) {
//     let buf_reader = BufReader::new(&stream);

//     let request_line = buf_reader.lines().next().unwrap().unwrap();

//     let (status_line, content) = if request_line == "GET / HTTP/1.1" {
//         ("HTTP/1.1 200 OK", "<h1>Hello!!</h1>")
//     } else if request_line == "GET /sleep HTTP/1.1" {
//         thread::sleep(Duration::from_secs(5));

//         ("HTTP/1.1 200 OK", "<h1>SLEEP!</h1>")
//     } else {
//         ("HTTP/1.1 404 NOT FOUND", "<h1>404 - Not found</h1>")
//     };

//     let length = content.len();
//     let response = format!("{status_line}\r\nContent-Length: {length}\r\n\r\n{content}");

//     stream.write_all(response.as_bytes()).unwrap();
// }
