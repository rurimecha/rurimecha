#!/bin/sh
set -eu
umask 022

listen_ip=0.0.0.0
listen_port=8080
configure_network=false
while [ "$#" -gt 0 ]; do
    case "$1" in
        --listen-ip|--listen-port)
            if [ "$#" -lt 2 ]; then echo "缺少 $1 的值。" >&2; exit 2; fi
            case "$1" in
                --listen-ip) listen_ip=$2 ;;
                --listen-port) listen_port=$2 ;;
            esac
            configure_network=true
            shift 2 ;;
        --help) echo 'Usage: sudo ./install.sh [--listen-ip IP] [--listen-port PORT]'; exit 0 ;;
        *) echo "不支持的参数：$1" >&2; exit 2 ;;
    esac
done
if [ "$(id -u)" -ne 0 ]; then
    echo 'Usage: sudo ./install.sh [--listen-ip IP] [--listen-port PORT]' >&2
    exit 2
fi

here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
program=/opt/rurimecha
unit=rurimecha.service
data=/var/lib/rurimecha

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

if [ "$configure_network" = false ] && [ -t 0 ]; then
    keep_existing=n
    if [ -f "$data/network.json" ]; then
        printf '保留已有监听地址和端口？[Y/n] '
        read -r keep_existing
        keep_existing=${keep_existing:-y}
    fi
    case "$keep_existing" in
        y|Y) ;;
        *)
            printf '监听 IP [0.0.0.0]：'
            read -r answer
            listen_ip=${answer:-0.0.0.0}
            printf '监听端口 [8080]：'
            read -r answer
            listen_port=${answer:-8080}
            configure_network=true ;;
    esac
fi
if [ ! -f "$data/network.json" ]; then configure_network=true; fi
case "$listen_ip" in
    *:*) endpoint="[$listen_ip]:$listen_port" ;;
    *) endpoint="$listen_ip:$listen_port" ;;
esac
check_endpoint=saved
if [ "$configure_network" = true ]; then check_endpoint=$endpoint; fi

was_running=false
if systemctl is-active --quiet "$unit"; then was_running=true; fi
if systemctl cat "$unit" >/dev/null 2>&1; then
    systemctl stop "$unit"
fi
if ! "$here/rurimecha-core" -data-dir "$data" -check-listen "$check_endpoint"; then
    echo '监听地址或端口不可用。请确认 IP 属于本机、端口未被占用，再重新安装；已有网络配置未修改。' >&2
    if [ "$was_running" = true ]; then systemctl start "$unit"; fi
    exit 1
fi

if ! id -u rurimecha >/dev/null 2>&1; then
    useradd --system --user-group --home-dir "$data" --shell /usr/sbin/nologin rurimecha
fi
install -d -o rurimecha -g rurimecha -m 0700 "$data"
install -d -m 0755 "$program"
install -m 0755 "$here/rurimecha-core" "$here/reset-admin-password.sh" "$program/"
cp -R "$here/media-tools" "$here/third_party" "$program/"
if [ "$configure_network" = true ]; then
    if ! runuser -u rurimecha -- "$program/rurimecha-core" -data-dir "$data" -configure-listen "$endpoint"; then
        echo '未能保存监听设置，安装已停止。' >&2
        if [ "$was_running" = true ]; then systemctl start "$unit"; fi
        exit 1
    fi
fi

install -m 0644 "$here/$unit" "/etc/systemd/system/$unit"
systemctl daemon-reload
systemctl enable --now "$unit"

echo 'Rurimecha 已安装。请使用所配置的监听地址和端口访问；监听全部地址时，使用服务器的局域网 IP。'
echo "For first-time setup, run: sudo journalctl -u rurimecha.service -g 'Ruri administrator setup code:' -n 1 --no-pager"
