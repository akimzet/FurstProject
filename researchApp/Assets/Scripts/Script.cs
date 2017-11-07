//  
// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
//

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR.WSA;

using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HoloLensCameraStream;

/// <summary>
/// This example gets the video frames at 30 fps and displays them on a Unity texture,
/// which is locked the User's gaze.
/// </summary>
public class Script : MonoBehaviour
{
    //For Hololens Display
    public Text DebugLabel;
    public Text DataLabel;
    public Text Target;
    public GameObject border;
    //For Setup for Update
    float interval = 0.5f;
    float nextTime = 5;
    //Stream
    byte[] _latestImageBytes;
    byte[] data;
    HoloLensCameraStream.Resolution _resolution;
    VideoCapture _videoCapture;
    IntPtr _spatialCoordinateSystemPtr;
    //Connection Variables
#if !UNITY_EDITOR
    Windows.Networking.Sockets.StreamSocket socket;
#endif
    //Reading and Writing to Socket
    string response;
    Stream streamIn, streamOut;
#if !UNITY_EDITOR

    void Start()
    {
        //Find Hololens Label
        DebugLabel.text = "Initialized Debug Log";
        DataLabel.text = "Initialized Data Log";
        //Create the StreamSocket and establish a connection to the echo server.
        socket = new Windows.Networking.Sockets.StreamSocket();
        Windows.Networking.HostName serverHost = new Windows.Networking.HostName("129.8.232.106");
        string serverPort = "11000";
        socket.ConnectAsync(serverHost, serverPort);
        //Fetch a pointer to Unity's spatial coordinate system if you need pixel mapping
        _spatialCoordinateSystemPtr = WorldManager.GetNativeISpatialCoordinateSystemPtr();
        //Call this in Start() to ensure that the CameraStreamHelper is already "Awake".
        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);
    }

    private async void WriteRead()
    {
        DebugLabel.text = "Convert to RGBA";
        //Convert BGRA32 to RGBA
        int stride = 4;
        float denominator = 1.0f / 255.0f;
        List<Color> colorArray = new List<Color>();
        for (int i = _latestImageBytes.Length - 1; i >= 0; i -= stride)
        {
            float a = (int)(_latestImageBytes[i - 0]) * denominator;
            float r = (int)(_latestImageBytes[i - 1]) * denominator;
            float g = (int)(_latestImageBytes[i - 2]) * denominator;
            float b = (int)(_latestImageBytes[i - 3]) * denominator;

            colorArray.Add(new Color(r, g, b, a));
        }
        Texture2D textureData = new Texture2D(_resolution.width, _resolution.height);
        textureData.SetPixels(colorArray.ToArray());
        textureData.Apply();
        //Crop Picture
        DebugLabel.text = "Cropping Picture";
        //Color[] c = textureData.GetPixels(352, 198, 900, 548);
        Color[] c = textureData.GetPixels(300, 150, 900, 548);
        Texture2D rightFit = new Texture2D(900, 548);
        rightFit.SetPixels(c);
        rightFit.Apply();
        //Turn Texture to ByteArray to Send
        data = rightFit.EncodeToJPG();
        //Free Memory
        DebugLabel.text = "Freeing Memory";
        Destroy(textureData);
        Destroy(rightFit);
        //Hololens Screen: 1268 x 720
        //Picture Size: 1408 x 792

        if (data != null)
        {
            DebugLabel.text = "Communicating with Server";
            //Write data to the echo server.
            streamOut = socket.OutputStream.AsStreamForWrite();
            BinaryWriter writer = new BinaryWriter(streamOut);
            writer.Write(data);
            writer.Flush();
            //Read data from the echo server.
            //streamIn = socket.InputStream.AsStreamForRead();
            //StreamReader reader = new StreamReader(streamIn);
            //response = reader.ReadLine();
            ////Parse String Received
            //var names = response.Split(' ');
            ////Get Location from Array - Starts at Bottom Left (0,0) to Top Right
            //Target.text = names[0];
            //float top = float.Parse(names[1]);
            //float right = float.Parse(names[2]);
            //float bottom = float.Parse(names[3]);
            //float left = float.Parse(names[4]);
            //DataLabel.text = "Top: " + top + " " + "Right: " + right + " " + "Bottom: " + bottom + " " + "Left: " + left;
            ////Calculate Position of Face on Hololens
            //float width = ((right - left) / 2.0f) + left;
            //float height = ((bottom - top) / 2.0f) + top;
            ////Calculate Position of Face Border on Hololens
            //float lilwidth = ((right - left) / 2.0f);
            //float lilheight = ((bottom - top) / 2.0f);
            ////Translate Picture Values to Hololens Resolution Values
            //width = (width * (1268.0f / 900.0f)) / 10;
            //height = (height * (720.0f / 548.0f)) / 10;
            //lilwidth = (lilwidth * (1268.0f / 900.0f)) / 10;
            //lilheight = (lilheight * (720.0f / 548.0f)) / 10;
            ////Move Target Label to Location Given
            //Vector3 position = new Vector3(width, height - 20, 0);
            //Target.GetComponent<RectTransform>().anchoredPosition = position;
            ////Create Borders with Position Given - Set Width and Height
            //border.GetComponent<RectTransform>().anchoredPosition = position;
            //border.GetComponent<RectTransform>().sizeDelta = new Vector2(lilwidth * 2, lilheight * 2);

            //DataLabel.text = "Width: " + width + " " + "Height: " + height;
            //Hololens Screen: 1268 x 720
            //Picture Size: 896 x 504
        }
    }

    private void OnDestroy()
    {
        if (_videoCapture != null)
        {
            //_videoCapture.FrameSampleAcquired -= OnFrameSampleAcquired;
            _videoCapture.Dispose();
        }
    }

    void OnVideoCaptureCreated(VideoCapture videoCapture)
    {
        if (videoCapture == null)
        {
            Debug.LogError("Did not find a video capture object. You may not be using the HoloLens.");
            return;
        }

        this._videoCapture = videoCapture;

        //Request the spatial coordinate ptr if you want fetch the camera and set it if you need to 
        CameraStreamHelper.Instance.SetNativeISpatialCoordinateSystemPtr(_spatialCoordinateSystemPtr);

        //_resolution = CameraStreamHelper.Instance.GetLowestResolution();
        _resolution = CameraStreamHelper.Instance.GetHighestResolution(); //Cause delay because of size
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(_resolution);
        //videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        //You don't need to set all of these params.
        //I'm just adding them to show you that they exist.
        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolution.height;
        cameraParams.cameraResolutionWidth = _resolution.width;
        cameraParams.frameRate = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat = CapturePixelFormat.BGRA32;
        //cameraParams.rotateImage180Degrees = true; //If your image is upside down, remove this line.
        cameraParams.enableHolograms = false;
        //Video Resolution 896 x 504
        videoCapture.StartVideoModeAsync(cameraParams, OnVideoModeStarted);
    }

    void OnVideoModeStarted(VideoCaptureResult result)
    {
        if (result.success == false)
        {
            Debug.LogWarning("Could not start video mode.");
            return;
        }

        Debug.Log("Video capture started.");
    }

    void OnFrameSampleAcquired(VideoCaptureSample sample)
    {
        DebugLabel.text = "OnFrameSampleAcquired Func";
        //When copying the bytes out of the buffer, you must supply a byte[] that is appropriately sized.
        //You can reuse this byte[] until you need to resize it (for whatever reason).
        if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength)
        {
            _latestImageBytes = new byte[sample.dataLength];
        }
        sample.CopyRawImageDataIntoBuffer(_latestImageBytes);
        sample.Dispose();

        //This is where we actually use the image data
        UnityEngine.WSA.Application.InvokeOnAppThread(() =>
        {
            WriteRead();
        }, false);
    }

    //Update per Frame
    void Update()
    {

        //Update per Second
        if (Time.time >= nextTime)
        {
            _videoCapture.RequestNextFrameSample(OnFrameSampleAcquired);
            nextTime += interval;
        }
    }
#endif
}
