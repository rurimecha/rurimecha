#!/bin/sh
set -eu
cd -- "$(dirname -- "$0")"
running=$(docker compose ps --status running -q core)
if [ -n "$running" ]; then
    docker compose stop core
fi

result=0
docker compose run --rm --no-deps -T core -data-dir /data -reset-admin-password || result=$?
if [ -n "$running" ]; then
    docker compose start core || exit 1
fi
exit "$result"
