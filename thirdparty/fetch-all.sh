#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

"$SCRIPT_DIR/fetch-sdl3-cs.sh"
"$SCRIPT_DIR/fetch-box3d.sh"
"$SCRIPT_DIR/fetch-imgui-net.sh"
"$SCRIPT_DIR/fetch-blurgtext.sh"
"$SCRIPT_DIR/fetch-simplemesh.sh"
"$SCRIPT_DIR/fetch-wattlescript.sh"
