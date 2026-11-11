#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Check the packaged tools, including seeking on an inherited file descriptor."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile


def run(tool, *args, stdin=None):
    return subprocess.run(
        [*tool, *map(str, args)], stdin=stdin, stdout=subprocess.PIPE,
        stderr=subprocess.PIPE, check=True, timeout=60,
    ).stdout


def inspect(probe, path):
    with path.open("rb") as media:
        return json.loads(run(
            probe, "-v", "error", "-protocol_whitelist", "fd,pipe",
            "-show_format", "-show_streams", "-of", "json", "-i", "fd:", stdin=media,
        ))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("platform", choices=["linux-amd64", "linux-arm64", "windows-amd64"])
    parser.add_argument("directory", type=Path)
    parser.add_argument("--runner", nargs="+", default=[])
    args = parser.parse_args()
    root = args.directory.resolve()
    suffix = ".exe" if args.platform == "windows-amd64" else ""
    paths = [root / "bin" / (name + suffix) for name in ("ffmpeg", "ffprobe")]
    expected = {}
    for line in (root / "SHA256SUMS").read_text().splitlines():
        digest, name = line.split(None, 1)
        expected[name] = digest
    for path in paths:
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != expected[path.relative_to(root).as_posix()]:
            raise RuntimeError(f"Checksum mismatch for {path}")

    ffmpeg, ffprobe = ([*args.runner, str(path)] for path in paths)
    for tool in (ffmpeg, ffprobe):
        if b"9.0.2-rurimecha.1" not in run(tool, "-version").splitlines()[0]:
            raise RuntimeError(f"Unexpected version for {tool[-1]}")
    config = run(ffmpeg, "-hide_banner", "-buildconf").decode()
    for flag in ("--enable-gpl", "--enable-version3", "--enable-libx264", "--enable-libmp3lame",
                 "--enable-libopus", "--enable-libvorbis", "--enable-zlib"):
        if flag not in config:
            raise RuntimeError(f"Missing {flag}")
    if "--enable-nonfree" in config:
        raise RuntimeError("Unredistributable FFmpeg configuration")

    with tempfile.TemporaryDirectory() as tmp:
        directory = Path(tmp)
        source = directory / "source.mp4"
        subtitle = directory / "source.srt"
        subtitle.write_text("1\n00:00:00,000 --> 00:00:01,000\nRuri\n", encoding="utf-8")
        run(ffmpeg, "-v", "error", "-f", "lavfi", "-i", "testsrc2=s=32x24:r=10",
            "-f", "lavfi", "-i", "sine=frequency=440", "-i", subtitle,
            "-t", "2", "-c:v", "libx264", "-c:a", "aac", "-c:s", "mov_text", source)
        data = source.read_bytes()
        if b"mdat" not in data or data.find(b"moov") <= data.find(b"mdat"):
            raise RuntimeError("Fixture needs an MP4 index after the media data")
        info = inspect(ffprobe, source)
        if float(info["format"]["duration"]) < 1.9:
            raise RuntimeError("Failed to read tail-index MP4")
        for name, encoder, codec in (
            ("test.mp3", "libmp3lame", "mp3"),
            ("test.opus", "libopus", "opus"),
            ("test.ogg", "libvorbis", "vorbis"),
        ):
            output = directory / name
            with source.open("rb") as media:
                run(ffmpeg, "-nostdin", "-v", "error", "-protocol_whitelist", "fd,pipe",
                    "-ss", "1", "-i", "fd:", "-t", "0.5", "-vn", "-sn", "-af", "loudnorm",
                    "-c:a", encoder, output, stdin=media)
            if inspect(ffprobe, output)["streams"][0]["codec_name"] != codec:
                raise RuntimeError(f"Failed to encode {codec}")
        with source.open("rb") as media:
            image = run(ffmpeg, "-nostdin", "-v", "error", "-protocol_whitelist", "fd,pipe",
                        "-ss", "1", "-i", "fd:", "-map", "0:v:0", "-frames:v", "1",
                        "-c:v", "png", "-f", "image2pipe", "pipe:1", stdin=media)
        if not image.startswith(b"\x89PNG\r\n\x1a\n"):
            raise RuntimeError("Failed to encode PNG preview")
        with source.open("rb") as media:
            text = run(ffmpeg, "-nostdin", "-v", "error", "-protocol_whitelist", "fd,pipe",
                       "-i", "fd:", "-map", "0:s:0", "-c:s", "webvtt", "-f", "webvtt", "pipe:1", stdin=media)
        if b"Ruri" not in text:
            raise RuntimeError("Failed to extract embedded subtitle")

    print(f"{args.platform}: checksums, configuration, probe, seek, encoding, preview and subtitles passed")


if __name__ == "__main__":
    main()
