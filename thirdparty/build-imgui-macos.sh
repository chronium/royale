#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SOURCE_DIR="$SCRIPT_DIR/repos/ImGui.Net/NativeLibraries"
BUILD_DIR="$SCRIPT_DIR/build/imgui/osx-arm64"
INSTALL_DIR="$SCRIPT_DIR/artifacts/imgui/osx-arm64"
EXPECTED_DYLIB="$INSTALL_DIR/lib/libroyale_imgui.dylib"

if [ "$(uname -s)" != "Darwin" ]; then
    echo "build-imgui-macos.sh requires macOS (Darwin)." >&2
    exit 1
fi

if [ "$(uname -m)" != "arm64" ]; then
    echo "build-imgui-macos.sh currently builds only macOS ARM64 artifacts." >&2
    exit 1
fi

sh "$SCRIPT_DIR/fetch-imgui-net.sh"

if ! pkg-config --exists sdl3; then
    echo "SDL3 development headers were not found by pkg-config." >&2
    echo "Install SDL3 headers locally before building the ImGui SDL3 backend." >&2
    exit 1
fi

mkdir -p "$BUILD_DIR" "$INSTALL_DIR/lib"

SDL_CFLAGS=$(pkg-config --cflags sdl3)
CIMGUI_DIR="$SOURCE_DIR/cimgui"
IMGUI_DIR="$CIMGUI_DIR/imgui"

clang++ \
    -std=c++17 \
    -arch arm64 \
    -dynamiclib \
    -undefined dynamic_lookup \
    -install_name "@rpath/libroyale_imgui.dylib" \
    -DIMNODES_NAMESPACE=imnodes \
    -DIMGUI_DISABLE_OBSOLETE_FUNCTIONS=1 \
    -DCIMGUI_VARGS0 \
    $SDL_CFLAGS \
    -I"$CIMGUI_DIR" \
    -I"$IMGUI_DIR" \
    -I"$IMGUI_DIR/backends" \
    -I"$SOURCE_DIR/cimplot" \
    -I"$SOURCE_DIR/cimplot/implot" \
    -I"$SOURCE_DIR/cimnodes" \
    -I"$SOURCE_DIR/cimnodes/imnodes" \
    -I"$SOURCE_DIR/cimguizmo" \
    -I"$SOURCE_DIR/cimguizmo/ImGuizmo" \
    "$CIMGUI_DIR/cimgui.cpp" \
    "$IMGUI_DIR/imgui.cpp" \
    "$IMGUI_DIR/imgui_draw.cpp" \
    "$IMGUI_DIR/imgui_demo.cpp" \
    "$IMGUI_DIR/imgui_widgets.cpp" \
    "$IMGUI_DIR/imgui_tables.cpp" \
    "$IMGUI_DIR/backends/imgui_impl_sdl3.cpp" \
    "$IMGUI_DIR/backends/imgui_impl_sdlgpu3.cpp" \
    "$SOURCE_DIR/cimplot/cimplot.cpp" \
    "$SOURCE_DIR/cimplot/implot/implot.cpp" \
    "$SOURCE_DIR/cimplot/implot/implot_demo.cpp" \
    "$SOURCE_DIR/cimplot/implot/implot_items.cpp" \
    "$SOURCE_DIR/cimnodes/cimnodes.cpp" \
    "$SOURCE_DIR/cimnodes/imnodes/imnodes.cpp" \
    "$SOURCE_DIR/cimguizmo/cimguizmo.cpp" \
    "$SOURCE_DIR/cimguizmo/ImGuizmo/ImGuizmo.cpp" \
    "$SOURCE_DIR/cimguizmo/ImGuizmo/GraphEditor.cpp" \
    "$SOURCE_DIR/cimguizmo/ImGuizmo/ImCurveEdit.cpp" \
    "$SOURCE_DIR/cimguizmo/ImGuizmo/ImGradient.cpp" \
    "$SOURCE_DIR/cimguizmo/ImGuizmo/ImSequencer.cpp" \
    "$SCRIPT_DIR/royale_imgui/royale_imgui.cpp" \
    -o "$EXPECTED_DYLIB"

if [ ! -f "$EXPECTED_DYLIB" ]; then
    echo "Expected ImGui shared library was not produced: $EXPECTED_DYLIB" >&2
    exit 1
fi

printf 'ImGui macOS ARM64 shared library installed to %s\n' "$EXPECTED_DYLIB"
