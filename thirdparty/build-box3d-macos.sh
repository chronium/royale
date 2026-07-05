#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SOURCE_DIR="$SCRIPT_DIR/repos/box3d"
BUILD_DIR="$SCRIPT_DIR/build/box3d/osx-arm64"
INSTALL_DIR="$SCRIPT_DIR/artifacts/box3d/osx-arm64"
EXPECTED_DYLIB="$INSTALL_DIR/lib/libbox3d.dylib"

if [ "$(uname -s)" != "Darwin" ]; then
    echo "build-box3d-macos.sh requires macOS (Darwin)." >&2
    exit 1
fi

if [ "$(uname -m)" != "arm64" ]; then
    echo "build-box3d-macos.sh currently builds only macOS ARM64 artifacts." >&2
    exit 1
fi

sh "$SCRIPT_DIR/fetch-box3d.sh"

if [ ! -d "$SOURCE_DIR" ]; then
    echo "Box3D source was not found at $SOURCE_DIR after fetch." >&2
    exit 1
fi

cmake -S "$SOURCE_DIR" \
    -B "$BUILD_DIR" \
    -G "Unix Makefiles" \
    -DCMAKE_BUILD_TYPE=Release \
    -DBUILD_SHARED_LIBS=ON \
    -DBOX3D_SAMPLES=OFF \
    -DBOX3D_UNIT_TESTS=OFF \
    -DBOX3D_BENCHMARKS=OFF \
    -DBOX3D_DOCS=OFF \
    -DBOX3D_PROFILE=OFF \
    -DBOX3D_VALIDATE=ON \
    -DCMAKE_INSTALL_PREFIX="$INSTALL_DIR"

cmake --build "$BUILD_DIR" --target install

if [ ! -f "$EXPECTED_DYLIB" ]; then
    echo "Expected Box3D shared library was not produced: $EXPECTED_DYLIB" >&2
    exit 1
fi

printf 'Box3D macOS ARM64 shared library installed to %s\n' "$EXPECTED_DYLIB"
