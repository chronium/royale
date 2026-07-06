using System.Numerics;

namespace Royale.Client.Rendering.Text;

public static class TextPixelTransform
{
    public static Vector2 ToClipSpace(Vector2 pixelPosition, uint swapchainWidth, uint swapchainHeight)
    {
        if (swapchainWidth == 0)
            throw new ArgumentOutOfRangeException(nameof(swapchainWidth), "Swapchain width must be non-zero.");

        if (swapchainHeight == 0)
            throw new ArgumentOutOfRangeException(nameof(swapchainHeight), "Swapchain height must be non-zero.");

        return new Vector2(
            (pixelPosition.X / swapchainWidth * 2.0f) - 1.0f,
            1.0f - (pixelPosition.Y / swapchainHeight * 2.0f));
    }
}
