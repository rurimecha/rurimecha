#!/usr/bin/env bash
set -euo pipefail

target=${1:?target is required}
jobs=${JOBS:-$(nproc)}
prefix=/dependencies
build_root=/build
out=/output
suffix=
cross=
ffmpeg_target=()
x264_target=()

case "$target" in
    linux-amd64)
        host=x86_64-linux-gnu
        ;;
    linux-arm64)
        host=aarch64-linux-gnu
        cross="$host-"
        ffmpeg_target=(--enable-cross-compile --pkg-config=pkg-config --arch=aarch64 --target-os=linux "--cross-prefix=$cross")
        ;;
    windows-amd64)
        host=x86_64-w64-mingw32
        cross="$host-"
        suffix=.exe
        ffmpeg_target=(--enable-cross-compile --pkg-config=pkg-config --arch=x86_64 --target-os=mingw32 "--cross-prefix=$cross" --extra-ldexeflags=-static --extra-libs=-lssp)
        ;;
    *)
        echo "Unsupported platform $target" >&2
        exit 2
        ;;
esac

if [[ -n "$cross" ]]; then
    x264_target=("--cross-prefix=$cross")
fi

export CC="${cross}gcc" AR="${cross}ar" RANLIB="${cross}ranlib" STRIP="${cross}strip"
export PKG_CONFIG_LIBDIR="$prefix/lib/pkgconfig"
export LC_ALL=C.UTF-8

(cd /sources && sha256sum --check SHA256SUMS)
mkdir -p "$build_root" "$prefix" "$out/bin"

unpack() {
    local component=$1 archive=$2
    mkdir -p "$build_root/$component"
    tar -xzf "/sources/$archive" --strip-components=1 -C "$build_root/$component"
    cd "$build_root/$component"
}

autoconf_library() {
    local component=$1 archive=$2
    shift 2
    unpack "$component" "$archive"
    ./configure --build=x86_64-linux-gnu "--host=$host" "--prefix=$prefix" --disable-shared --enable-static "$@"
    make -j"$jobs"
    make install
}

unpack zlib zlib-1.3.2.tar.gz
CHOST="$host" ./configure --static "--prefix=$prefix"
make -j"$jobs" libz.a
make install

autoconf_library libogg libogg-1.3.6.tar.gz
autoconf_library libvorbis libvorbis-1.3.7.tar.gz --disable-oggtest
autoconf_library opus opus-1.6.1.tar.gz --disable-extra-programs --disable-doc
autoconf_library lame lame-3.100.tar.gz --disable-frontend --disable-decoder

unpack x264 x264-0480cb05.tar.gz
./configure "--host=$host" "${x264_target[@]}" "--prefix=$prefix" --enable-static \
    --disable-cli --disable-opencl --disable-lavf --disable-swscale
make -j"$jobs"
make install

unpack ffmpeg ffmpeg-d4070a14ce.tar.gz
./configure "${ffmpeg_target[@]}" "--prefix=$out" --extra-version=rurimecha.1 \
    "--extra-cflags=-I$prefix/include" "--extra-ldflags=-L$prefix/lib" \
    --pkg-config-flags=--static --disable-autodetect --disable-debug --disable-doc --disable-ffplay \
    --disable-shared --enable-static --enable-gpl --enable-version3 \
    --enable-libx264 --enable-libmp3lame --enable-libopus --enable-libvorbis --enable-zlib || { tail -n 100 ffbuild/config.log; exit 1; }
make -j"$jobs" "ffmpeg$suffix" "ffprobe$suffix"
install -m 755 "ffmpeg$suffix" "ffprobe$suffix" "$out/bin/"
"$STRIP" --strip-unneeded "$out/bin/ffmpeg$suffix" "$out/bin/ffprobe$suffix"
sed -n 's/^#define FFMPEG_CONFIGURATION "\(.*\)"$/\1/p' config.h > "$out/buildconf.txt"
dpkg-query -W > "$out/toolchain.txt"
cp -a /notices "$out/licenses"
if [[ "$target" == windows-amd64 ]]; then
    install -m 644 /usr/share/doc/gcc-mingw-w64-x86-64-posix/copyright "$out/licenses/MinGW-runtime.txt"
fi
(cd "$out" && sha256sum -- bin/* > SHA256SUMS)
