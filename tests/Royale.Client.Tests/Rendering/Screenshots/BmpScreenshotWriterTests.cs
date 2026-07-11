using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Text;
using SDL;

namespace Royale.Client.Tests.Rendering.Screenshots;

public sealed class BmpScreenshotWriterTests
{
    [Fact]
    public void SaveWritesBmpHeaderAndConvertsRgbaPixelsToBgra()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bmp");

        try
        {
            byte[] rgba =
            [
                0x11, 0x22, 0x33, 0x44,
            ];

            BmpScreenshotWriter.Save(path, rgba, 1, 1, SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM);

            byte[] bmp = File.ReadAllBytes(path);

            Assert.Equal((byte)'B', bmp[0]);
            Assert.Equal((byte)'M', bmp[1]);
            Assert.Equal(0x33, bmp[54]);
            Assert.Equal(0x22, bmp[55]);
            Assert.Equal(0x11, bmp[56]);
            Assert.Equal(0x44, bmp[57]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
