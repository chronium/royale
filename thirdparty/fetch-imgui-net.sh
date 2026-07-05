#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$SCRIPT_DIR/versions.env"

DEST="$SCRIPT_DIR/repos/ImGui.Net"
PATCH_DIR="$SCRIPT_DIR/patches/ImGui.Net"

if [ ! -d "$DEST/.git" ]; then
    mkdir -p "$DEST"
    git -C "$DEST" init
fi

if git -C "$DEST" remote get-url origin >/dev/null 2>&1; then
    git -C "$DEST" remote set-url origin "$IMGUI_NET_REPO"
else
    git -C "$DEST" remote add origin "$IMGUI_NET_REPO"
fi

git -C "$DEST" fetch --depth 1 origin "$IMGUI_NET_COMMIT"
git -C "$DEST" checkout --detach FETCH_HEAD
git -C "$DEST" reset --hard FETCH_HEAD
git -C "$DEST" clean -xfd

if [ -d "$PATCH_DIR" ]; then
    for patch in "$PATCH_DIR"/*.patch; do
        [ -e "$patch" ] || continue
        git -C "$DEST" apply --3way "$patch"
    done
fi
