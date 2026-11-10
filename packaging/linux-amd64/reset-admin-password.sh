#!/bin/sh
set -eu
if [ "$#" -gt 1 ]; then
    echo "Usage: $0 [data-directory]" >&2
    exit 2
fi
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec "$here/rurimecha-core" -data-dir "${1:-$here/data}" -reset-admin-password
