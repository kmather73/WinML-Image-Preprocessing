using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.UI.Xaml.Media.Imaging;
using Windows.AI.MachineLearning;
using System.IO;

namespace ImagePreprocessingWinML.Scripts
{
    public static class PreprocessVideoFrame
    {
        public static async Task<TensorFloat> NormalizeImage(VideoFrame frame, Vector3 mean, Vector3 std, uint width, uint height)
        {
            // , BitmapPixelFormat.Bgra8
            var bitmapBuffer = new SoftwareBitmap(frame.SoftwareBitmap.BitmapPixelFormat, frame.SoftwareBitmap.PixelHeight, frame.SoftwareBitmap.PixelHeight, BitmapAlphaMode.Ignore);
	        var buffer = VideoFrame.CreateWithSoftwareBitmap(bitmapBuffer);
	        await frame.CopyToAsync(buffer);


            var innerBitmap = new WriteableBitmap(bitmapBuffer.PixelWidth, bitmapBuffer.PixelHeight);
            bitmapBuffer.CopyToBuffer(innerBitmap.PixelBuffer);
            var pixelsStream = innerBitmap.PixelBuffer.AsStream();

            var transform = new BitmapTransform() { ScaledWidth = width, ScaledHeight = height, InterpolationMode = BitmapInterpolationMode.Cubic };
            var decoder = await BitmapDecoder.CreateAsync(pixelsStream.AsRandomAccessStream());
            var pixelData = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
            var pixels = pixelData.DetachPixelData();

            return Normalize(pixels, mean, std, width, height);
        }

        private static TensorFloat Normalize(byte[] src, System.Numerics.Vector3 mean, System.Numerics.Vector3 std, uint width, uint height)
        {
            var normalized = new float[src.Length / 4 * 3];
            for (int i = 0; i < src.Length / 4; i++)
            {
                var val = src[i];
                normalized[i * 3 + 0] = ((src[4*i] / 255f) - mean.X) / std.X;
                normalized[i * 3 + 1] = ((src[4 * i + 1] / 255f) - mean.Y) / std.Y;
                normalized[i * 3 + 2] = ((src[4 * i + 2] / 255f) - mean.Z) / std.Z;
            }
            var shape = new List<long> { 3, width, height };
            return TensorFloat.CreateFromArray(shape, normalized);
        }
    }
}
