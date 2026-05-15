#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

pids=()

cleanup() {
  for pid in "${pids[@]:-}"; do
    kill "$pid" >/dev/null 2>&1 || true
  done
}

trap cleanup EXIT INT TERM

echo "Starting nats-server..."
nats-server &
pids+=("$!")

echo "Starting ingestion-api..."
(cd "$ROOT_DIR/ingestion-api" && cargo run) &
pids+=("$!")

echo "Starting processor..."
(cd "$ROOT_DIR/processor" && cargo run) &
pids+=("$!")

wait
