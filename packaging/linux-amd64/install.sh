#!/bin/sh
set -eu
umask 022

if [ "$#" -ne 0 ] || [ "$(id -u)" -ne 0 ]; then
    echo 'Usage: sudo ./install.sh' >&2
    exit 2
fi

here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
program=/opt/rurimecha
unit=rurimecha.service

if [ ! -x "$here/rurimecha-core" ] || [ ! -x "$here/media-tools/bin/ffmpeg" ] ||
   [ ! -x "$here/media-tools/bin/ffprobe" ] || [ ! -f "$here/$unit" ] ||
   [ ! -f "$here/reset-admin-password.sh" ] || [ ! -d "$here/third_party" ]; then
    echo 'Extract the complete Rurimecha Linux release, then run install.sh from that directory.' >&2
    exit 1
fi

if ! systemctl show --property=Version --value >/dev/null 2>&1; then
    echo 'This installer requires a running systemd system. For a manual start, run ./rurimecha-core -data-dir ./data.' >&2
    exit 1
fi

if ! id -u rurimecha >/dev/null 2>&1; then
    useradd --system --user-group --home-dir /var/lib/rurimecha --shell /usr/sbin/nologin rurimecha
fi
if systemctl cat "$unit" >/dev/null 2>&1; then
    systemctl stop "$unit"
fi

install -d -m 0755 "$program"
install -m 0755 "$here/rurimecha-core" "$here/reset-admin-password.sh" "$program/"
cp -R "$here/media-tools" "$here/third_party" "$program/"
install -m 0644 "$here/$unit" "/etc/systemd/system/$unit"
systemctl daemon-reload
systemctl enable --now "$unit"

echo 'Rurimecha is installed. Open http://127.0.0.1:8080/ on this computer, or use your configured address.'
echo "For first-time setup, run: sudo journalctl -u rurimecha.service -g 'Ruri administrator setup code:' -n 1 --no-pager"
