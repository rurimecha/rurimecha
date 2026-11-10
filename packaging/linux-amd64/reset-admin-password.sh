#!/bin/sh
set -eu
if [ "$#" -gt 1 ]; then
    echo "Usage: $0 [data-directory | --service]" >&2
    exit 2
fi
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if [ "${1:-}" = '--service' ]; then
    if [ "$(id -u)" -ne 0 ]; then
        echo "Run sudo $0 --service to reset the installed service administrator password." >&2
        exit 2
    fi
    unit=rurimecha.service
    running=$(systemctl show --property=ActiveState --value "$unit")
    systemctl stop "$unit"
    result=0
    runuser -u rurimecha -- /opt/rurimecha/rurimecha-core -data-dir /var/lib/rurimecha -reset-admin-password || result=$?
    if [ "$running" = 'active' ]; then
        systemctl start "$unit" || exit 1
    fi
    exit "$result"
fi
exec "$here/rurimecha-core" -data-dir "${1:-$here/data}" -reset-admin-password
