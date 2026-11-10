FROM ubuntu:22.04@sha256:8a2321a92b438238f93b4346fff61bc4259c99fdd4d9b52ee86f153fee0c344f AS toolchain
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        build-essential ca-certificates gcc-aarch64-linux-gnu libc6-dev-arm64-cross \
        gcc-mingw-w64-x86-64-posix nasm pkg-config \
    && rm -rf /var/lib/apt/lists/*

FROM toolchain AS builder
ARG TARGET
ARG JOBS
COPY sources/ /sources/
COPY licenses/ /notices/
COPY build.sh /usr/local/bin/build-ruri-ffmpeg
RUN JOBS="$JOBS" /usr/local/bin/build-ruri-ffmpeg "$TARGET"

FROM scratch
COPY --from=builder /output/ /
COPY SOURCE.txt /SOURCE.txt
