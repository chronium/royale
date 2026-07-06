#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SOURCE_DIR="$SCRIPT_DIR/repos/blurgtext"
BUILD_DIR="$SCRIPT_DIR/build/blurgtext/osx-arm64"
INSTALL_DIR="$SCRIPT_DIR/artifacts/blurgtext/osx-arm64"
EXPECTED_DYLIB="$INSTALL_DIR/lib/libblurgtext.dylib"

if [ "$(uname -s)" != "Darwin" ]; then
    echo "build-blurgtext-macos.sh requires macOS (Darwin)." >&2
    exit 1
fi

if [ "$(uname -m)" != "arm64" ]; then
    echo "build-blurgtext-macos.sh currently builds only macOS ARM64 artifacts." >&2
    exit 1
fi

sh "$SCRIPT_DIR/fetch-blurgtext.sh"

if [ ! -d "$SOURCE_DIR" ]; then
    echo "BlurgText source was not found at $SOURCE_DIR after fetch." >&2
    exit 1
fi

cmake -S "$SOURCE_DIR" \
    -B "$BUILD_DIR" \
    -G "Unix Makefiles" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DBUILD_SHARED_LIBS=ON \
    -DBT_BUILD_DEMO=OFF

cmake --build "$BUILD_DIR" --target blurgtext

mkdir -p "$INSTALL_DIR/lib"
cp "$BUILD_DIR/libblurgtext.dylib" "$EXPECTED_DYLIB"

if [ ! -f "$EXPECTED_DYLIB" ]; then
    echo "Expected BlurgText shared library was not produced: $EXPECTED_DYLIB" >&2
    exit 1
fi

printf 'BlurgText macOS ARM64 shared library installed to %s\n' "$EXPECTED_DYLIB"
