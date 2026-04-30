# Context
This project is a full observability system that monitor errors of dotnet web apps. It is in it's inital state. The HTTP errors are collected and then sent to the ingestion-api.

# Objective
Send the details of the failed requests of the project instrumentation/ to the ingestion-api, running at 127.0.0.1:7878.

## Requirements
- For sending the requests details, you must use only HTTP2.
- The failed requests need the be sent in batches, to avoid overloading the network.
- The performance needs to be very good. The application that is being monitored, must have almost no impact on latency or something else.
- The instrumentation must be implemented on the HttpLogger project (it will be used as a sdk on the future)

## Guidelines
- You are a very pragmatic architect and a senior engineer.
- You prioritize simplicity over complexity.
- You don't overengineer.
- You use the conventions and best practices for both languages and libraries.
