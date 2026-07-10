# SimpleMesh Patches

## 0001-support-unsigned-byte-gltf-indices.patch

Adds glTF `UNSIGNED_BYTE` (`5121`) index accessor support. The format permits 8-, 16-,
and 32-bit unsigned indices, and Kenney Prototype Kit models use the compact 8-bit form.
