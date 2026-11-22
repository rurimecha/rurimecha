#!/usr/bin/env bash
# Prepare the retained binaries for packaging without compiling FFmpeg.
set -euo pipefail
if (( $# != 2 )); then
    echo 'Usage: pack-prebuilt.sh EXISTING_FFMPEG_BUILD_DIRECTORY NEW_OUTPUT_DIRECTORY' >&2
    exit 2
fi
recipe=$(cd -- "$(dirname -- "$0")" && pwd)
source=$(cd -- "$1" && pwd)
output=$2
source_name=ruri-ffmpeg-9.0.2-rurimecha.1-sources.tar.gz
(cd "$source" && sha256sum --check --strict "$recipe/prebuilt.SHA256SUMS")
mkdir -- "$output"
cp -- "$source/$source_name" "$output/"
for target in linux-amd64 linux-arm64 windows-amd64; do
    tar -czf "$output/ruri-ffmpeg-$target.tar.gz" -C "$source/$target" .
done
(cd "$output" && sha256sum ruri-ffmpeg-*.tar.gz > SHA256SUMS)
