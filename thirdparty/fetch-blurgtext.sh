#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$SCRIPT_DIR/versions.env"

DEST="$SCRIPT_DIR/repos/blurgtext"
PATCH_DIR="$SCRIPT_DIR/patches/blurgtext"

if [ ! -d "$DEST/.git" ]; then
    mkdir -p "$DEST"
    git -C "$DEST" init
fi

if git -C "$DEST" remote get-url origin >/dev/null 2>&1; then
    git -C "$DEST" remote set-url origin "$BLURGTEXT_REPO"
else
    git -C "$DEST" remote add origin "$BLURGTEXT_REPO"
fi

git -C "$DEST" fetch --depth 1 origin "$BLURGTEXT_COMMIT"
git -C "$DEST" checkout --detach FETCH_HEAD
git -C "$DEST" reset --hard FETCH_HEAD
git -C "$DEST" clean -xfd
git -C "$DEST" submodule update --init --depth 1 deps/libraqm deps/SheenBidi deps/libunibreak deps/plutosvg
git -C "$DEST/deps/plutosvg" submodule update --init --depth 1 plutovg

if [ -d "$PATCH_DIR" ]; then
    for patch in "$PATCH_DIR"/*.patch; do
        [ -e "$patch" ] || continue
        git -C "$DEST" apply --3way "$patch"
    done
fi
