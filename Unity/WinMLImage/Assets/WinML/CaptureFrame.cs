using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_WSA && !UNITY_EDITOR
using Windows.Storage;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.Capture.Frames;
#endif

public class CaptureFrame : MonoBehaviour
{
    private GameObject Label;
    private TextMesh LabelText;
    private TimeSpan predictEvery = TimeSpan.FromMilliseconds(50);
    private string textToDisplay;
    private bool textToDisplayChanged;

#if WINDOWS_UWP
    MediaCapture MediaCapture;
#endif

    private void Start()
    {
        LabelText = Label.GetComponent<TextMesh>();

#if WINDOWS_UWP
        CreateMediaCapture();
        InitializeModel();
#else
        DisplayText("Does not work in player.");
#endif
    }

    private void DisplayText(string text)
    {
        textToDisplay = text;
        textToDisplayChanged = true;
    }

#if WINDOWS_UWP
    private async void InitializeModel()
    {
        StorageFile imageRecoModelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Data/StreamingAssets/MySuperCoolOnnxModel.onnx"));
    }

    private async void CreateMediaCapture()
    {
        MediaCapture = new MediaCapture();
        MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
        settings.StreamingCaptureMode = StreamingCaptureMode.Video;
        await MediaCapture.InitializeAsync(settings);

        CreateFrameReader();
    }

    private async void CreateFrameReader()
    {
        var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();

        MediaFrameSourceGroup selectedGroup = null;
        MediaFrameSourceInfo colorSourceInfo = null;

        foreach (var sourceGroup in frameSourceGroups)
        {
            foreach (var sourceInfo in sourceGroup.SourceInfos)
            {
                if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview
                    && sourceInfo.SourceKind == MediaFrameSourceKind.Color)
                {
                    colorSourceInfo = sourceInfo;
                    break;
                }
            }

            if (colorSourceInfo != null)
            {
                selectedGroup = sourceGroup;
                break;
            }
        }

        var colorFrameSource = MediaCapture.FrameSources[colorSourceInfo.Id];
        var preferredFormat = colorFrameSource.SupportedFormats.Where(format =>
        {
            return format.Subtype == MediaEncodingSubtypes.Argb32;
        }).FirstOrDefault();

        var mediaFrameReader = await MediaCapture.CreateFrameReaderAsync(colorFrameSource);
        await mediaFrameReader.StartAsync();
        StartPullFrames(mediaFrameReader);
    }

    private void StartPullFrames(MediaFrameReader sender)
    {
        Task.Run(async () =>
        {
            for (;;)
            {
                var frameReference = sender.TryAcquireLatestFrame();
                var videoFrame = frameReference?.VideoMediaFrame?.GetVideoFrame();
                if (videoFrame == null)
                {
                    continue; //ignoring frame
                }

                if(videoFrame.Direct3DSurface == null)
                {
                    continue; //ignoring frame
                }


                await Task.Delay(predictEvery);
            }

        });
    }
#endif

    private void Update()
    {
        if (textToDisplayChanged)
        {
            LabelText.text = textToDisplay;
            textToDisplayChanged = false;
        }
    }
}