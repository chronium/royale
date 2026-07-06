using System.Numerics;
using System.Runtime.InteropServices;

namespace Royale.Client.Rendering.Text;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TextScreenConstants(Vector2 SwapchainSize, Vector2 Padding)
{
    public TextScreenConstants(uint swapchainWidth, uint swapchainHeight)
        : this(new Vector2(swapchainWidth, swapchainHeight), Vector2.Zero)
    {
    }
}
