#if WINDOWS_UWP
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
namespace ImagePreprocessingWinML.Scripts
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public class PreprocessVideoEffect : IBasicVideoEffect
    {
        private VideoEncodingProperties m_encodingProperties;
        private IDirect3DDevice m_device;
        private CanvasDevice m_canvasDevice;

        private IPropertySet m_configuration;
        private Vector3 m_mean;
        private Vector3 m_std;

        public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            m_encodingProperties = encodingProperties;
            m_device = device;
            if (m_device != null)
            {
                m_canvasDevice = CanvasDevice.CreateFromDirect3D11Device(m_device);
            }
        }

        public void ProcessFrame(ProcessVideoFrameContext context)
        {
            if(context.InputFrame.SoftwareBitmap != null)
            {
                ProcessFrameCPU(context);
            }
            else if(context.InputFrame.Direct3DSurface != null)
            {
                ProcessFrameGPU(context);
            }
        }

        public unsafe void ProcessFrameCPU(ProcessVideoFrameContext context)
        {
            using (BitmapBuffer buffer = context.InputFrame.SoftwareBitmap.LockBuffer(BitmapBufferAccessMode.Read))
            using (BitmapBuffer targetBuffer = context.OutputFrame.SoftwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                using (var targetReference = targetBuffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacity;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacity);

                    byte* targetDataInBytes;
                    uint targetCapacity;
                    ((IMemoryBufferByteAccess)targetReference).GetBuffer(out targetDataInBytes, out targetCapacity);

                    // Fill-in the BGRA plane
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    var bytesPerPixel = 4;
                    for (int i = 0; i < bufferLayout.Height; i++)
                    {
                        for (int j = 0; j < bufferLayout.Width; j++)
                        {
                            if (m_encodingProperties.Subtype != "ARGB32")
                            {
                                // If you support other encodings, adjust index into the buffer accordingly
                            }

                            int idx = bufferLayout.StartIndex + bufferLayout.Stride * i + bytesPerPixel * j;
                            targetDataInBytes[idx + 0] = (byte)((dataInBytes[idx + 0] - m_mean.X) / m_std.X);
                            targetDataInBytes[idx + 1] = (byte)((dataInBytes[idx + 1] - m_mean.Y) / m_std.Y);
                            targetDataInBytes[idx + 2] = (byte)((dataInBytes[idx + 2] - m_mean.Z) / m_std.Z);
                            targetDataInBytes[idx + 3] = dataInBytes[idx + 3];
                        }
                    }
                }
            }
        }

        public void ProcessFrameGPU(ProcessVideoFrameContext context)
        {
            var inputSurface = context.InputFrame.Direct3DSurface;
            var outputSurface = context.OutputFrame.Direct3DSurface;

            using (var inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(m_canvasDevice, inputSurface))
            using (var renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(m_canvasDevice, outputSurface))
            using (var drawingSession = renderTarget.CreateDrawingSession())
            {
                var normalize = new Matrix5x4() { M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f, M51 = -m_mean.X, M52 = -m_mean.Y, M53 = -m_mean.Z};
                var normalize2 = new Matrix5x4() { M11 = 1 / m_std.X, M22 = 1 / m_std.Y, M33 = 1 / m_std.Z, M44 = 1.0f };

                // https://microsoft.github.io/Win2D/html/T_Microsoft_Graphics_Canvas_Effects_ColorMatrixEffect.htm
                var PreprocessTransfrom = new ColorMatrixEffect()
                {
                    ColorMatrix = normalize2,
                    Source = new ColorMatrixEffect()
                    {
                        ColorMatrix = normalize,
                        Source = inputBitmap
                    }
                };

                drawingSession.DrawImage(PreprocessTransfrom);
            }
        }

        public void Close(MediaEffectClosedReason reason)
        {
            // Only when using the GPU do we need to dispose of resources.
            if (m_canvasDevice != null)
            {
                m_canvasDevice.Dispose();
            }
        }

        public void DiscardQueuedFrames()
        {
            // Do nothing since were not saving previous frames 
        }

        public bool IsReadOnly => false;

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties
        {
            get
            {
                return new List<VideoEncodingProperties>()
                {
                    new VideoEncodingProperties()
                    {
                        Subtype = "ARGB32",
                    }
                };
            }
        }

        public MediaMemoryTypes SupportedMemoryTypes => MediaMemoryTypes.GpuAndCpu;

        public bool TimeIndependent => true;

        public void SetProperties(IPropertySet configuration)
        {
            m_configuration = configuration;
            Object meanObject;
            configuration.TryGetValue("mean", out meanObject);
            if (meanObject != null)
            {
                m_mean = (Vector3)meanObject;
            }
            else
            {
                m_mean = Vector3.Zero;
            }

            Object stdObject;
            configuration.TryGetValue("std", out stdObject);
            if(stdObject != null)
            {
                m_std = (Vector3)stdObject;
            }
            else
            {
                m_std = Vector3.One;
            }

        }
    }
}
#endif
