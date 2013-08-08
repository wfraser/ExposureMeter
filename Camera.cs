﻿using Microsoft.Phone.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Phone.Media.Capture;

namespace ExposureMeter
{
    public class Camera : DependencyObject
    {
        #region Dependency Properties
        public static readonly DependencyProperty PreviewVisibilityProperty = DependencyProperty.Register(
            "PreviewVisibility", typeof(Visibility), typeof(Camera), new PropertyMetadata(Visibility.Collapsed));
        public Visibility PreviewVisibility
        {
            get { return (Visibility)GetValue(PreviewVisibilityProperty); }
            set { SetValue(PreviewVisibilityProperty, value); }
        }

        public static readonly DependencyProperty CaptureVisibilityProperty = DependencyProperty.Register(
            "CaptureVisibility", typeof(Visibility), typeof(Camera), new PropertyMetadata(Visibility.Collapsed));
        public Visibility CaptureVisibility
        {
            get { return (Visibility)GetValue(CaptureVisibilityProperty); }
            set { SetValue(CaptureVisibilityProperty, value); }
        }

        public static readonly DependencyProperty CaptureImageProperty = DependencyProperty.Register(
            "CaptureImage", typeof(BitmapImage), typeof(Camera), new PropertyMetadata(null));
        public BitmapImage CaptureImage
        {
            get { return (BitmapImage)GetValue(CaptureImageProperty); }
            set { SetValue(CaptureImageProperty, value); }
        }

        public static readonly DependencyProperty PreviewBrushProperty = DependencyProperty.Register(
            "PreviewBrush", typeof(Brush), typeof(Camera), new PropertyMetadata(new SolidColorBrush(Colors.DarkGray)));
        public Brush PreviewBrush
        {
            get { return (Brush)GetValue(PreviewBrushProperty); }
            set { SetValue(PreviewBrushProperty, value); }
        }

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            "Orientation", typeof(int), typeof(Camera), new PropertyMetadata(0));
        public int Orientation
        {
            get { return (int)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public static readonly DependencyProperty ISOProperty = DependencyProperty.Register(
            "ISO", typeof(string), typeof(Camera), new PropertyMetadata(null));
        public string ISO
        {
            get { return (string)GetValue(ISOProperty); }
            set { SetValue(ISOProperty, value); }
        }

        public static readonly DependencyProperty ShutterSpeedProperty = DependencyProperty.Register(
            "ShutterSpeed", typeof(string), typeof(Camera), new PropertyMetadata(null));
        public string ShutterSpeed
        {
            get { return (string)GetValue(ShutterSpeedProperty); }
            set { SetValue(ShutterSpeedProperty, value); }
        }

        public static readonly DependencyProperty ApertureValueProperty = DependencyProperty.Register(
            "ApertureValue", typeof(string), typeof(Camera), new PropertyMetadata(null));
        public string ApertureValue
        {
            get { return (string)GetValue(ApertureValueProperty); }
            set { SetValue(ApertureValueProperty, value); }
        }
        #endregion

        public Camera()
        {
            m_desiredProperties = new Dictionary<Guid, object>
            {
                { KnownCameraPhotoProperties.FlashMode, FlashState.Off },
                { KnownCameraPhotoProperties.FocusIlluminationMode, FocusIlluminationMode.Off }
            };

            m_previewCaptureSource = new CaptureSource();

            App.RootFrame.OrientationChanged += RootFrame_OrientationChanged;
        }

        private Dictionary<Guid, object> m_desiredProperties;
        private MemoryStream m_captureStream;
        private int m_sensorOrientation;
        private CaptureSource m_previewCaptureSource;

        public async Task Initialize()
        {
            using (PhotoCaptureDevice device = await PhotoCaptureDevice.OpenAsync(CameraSensorLocation.Back, s_captureResolution))
            {
                m_sensorOrientation = (Int32)device.SensorRotationInDegrees;
            }
            Orientation = GetOrientation();
        }

        public void StartPreview()
        {
            CaptureVisibility = Visibility.Collapsed;
            ISO = null;

            VideoCaptureDevice videoDevice = CaptureDeviceConfiguration.GetDefaultVideoCaptureDevice();
            if (videoDevice != null)
            {
                var formats = videoDevice.SupportedFormats.ToList();
                m_previewCaptureSource.VideoCaptureDevice = videoDevice;

                var previewBrush = new VideoBrush();
                //previewBrush.Stretch = Stretch.Uniform;
                previewBrush.RelativeTransform = GetBrushRotation();
                previewBrush.SetSource(m_previewCaptureSource);

                m_previewCaptureSource.Start();

                PreviewBrush = previewBrush;
                PreviewVisibility = Visibility.Visible;
            }
        }

        public async Task StopPreview()
        {
            var capturedEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
            var handler = new EventHandler<CaptureImageCompletedEventArgs>((sender, e) =>
                {
                    WriteableBitmap result = e.Result;
                    var brush = new ImageBrush();
                    brush.ImageSource = result;
                    brush.RelativeTransform = GetBrushRotation();
                    PreviewBrush = brush;
                    capturedEvent.Set();
                });

            m_previewCaptureSource.CaptureImageCompleted += handler;
            m_previewCaptureSource.CaptureImageAsync();
            await Task.Run(() => capturedEvent.WaitOne());
            m_previewCaptureSource.CaptureImageCompleted -= handler;
            m_previewCaptureSource.Stop();
        }

        public async Task Capture()
        {
            try
            {
                await StopPreview();

                using (PhotoCaptureDevice device = await PhotoCaptureDevice.OpenAsync(CameraSensorLocation.Back, s_captureResolution))
                {
                    lock (m_desiredProperties)
                    {
                        foreach (KeyValuePair<Guid, object> pair in m_desiredProperties)
                        {
                            device.SetProperty(pair.Key, pair.Value);
                        }
                    }

                    device.SetProperty(KnownCameraGeneralProperties.EncodeWithOrientation, Orientation);

                    m_captureStream = new MemoryStream();
                    CameraCaptureSequence sequence = device.CreateCaptureSequence(1);
                    await device.PrepareCaptureSequenceAsync(sequence);
                    sequence.Frames[0].CaptureStream = m_captureStream.AsOutputStream();

                    await sequence.StartCaptureAsync();
                    CameraCaptureFrame frame = sequence.Frames[0];

                    m_captureStream.Seek(0, SeekOrigin.Begin);
                    var image = new BitmapImage();
                    image.SetSource(m_captureStream);

                    UInt32 iso = (UInt32)frame.AppliedProperties[KnownCameraPhotoProperties.Iso];
                    UInt32 exposureTimeUs = (UInt32)frame.AppliedProperties[KnownCameraPhotoProperties.ExposureTime];

                    ISO = iso.ToString();
                    ShutterSpeed = ExposureTimeToShutterSpeed(exposureTimeUs);
                    ApertureValue = FStopToString(HardwareFStop);

                    CaptureImage = image;
                    PreviewVisibility = Visibility.Collapsed;
                    CaptureVisibility = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                //TODO
            }
        }

        public void FixIso(UInt32? iso)
        {
            if (iso.HasValue)
            {
                m_desiredProperties[KnownCameraPhotoProperties.Iso] = iso.Value;
            }
            else
            {
                m_desiredProperties.Remove(KnownCameraPhotoProperties.Iso);
            }
        }

        public void FixExposureTime(UInt32? exposureTime)
        {
            if (exposureTime.HasValue)
            {
                m_desiredProperties[KnownCameraPhotoProperties.ExposureTime] = exposureTime.Value;
            }
            else
            {
                m_desiredProperties.Remove(KnownCameraPhotoProperties.ExposureTime);
            }
        }

        // TODO: this needs to be detected per-device.
        // It's correct for Lumia 920 and HTC 8X.
        private static readonly double HardwareFStop = 2.0;

        // These figures are actually scaled by the value in App.Current.Host.Content.ScaleFactor,
        // but for aspect ratio purposes, we don't care about that.
        private static double ScreenWidth = App.Current.Host.Content.ActualWidth;
        private static double ScreenHeight = App.Current.Host.Content.ActualHeight;

        // Find the capture resolution that's closest to the screen resolution.
        // If there's a tie, return the higher resolution.
        private static Windows.Foundation.Size s_captureResolution =
            PhotoCaptureDevice.GetAvailableCaptureResolutions(CameraSensorLocation.Back)
                .OrderBy(size => Math.Abs(size.Width / size.Height - ScreenHeight / ScreenWidth)) // Intentionally backwards: screen is portrait, sensor is landscape.
                .ThenByDescending(size => size.Width * size.Height)
                .First();

        // List of F-numbers traditionally listed on cameras, in 1/3 stop increments,
        // and the actual precise mathematical values they correspond to.
        public static readonly Dictionary<string, double> TraditionalToActualFNumbers = new Dictionary<string, double>
        {
            {"0.7", ApertureValueToFNumber(-1d)},
            {"0.8", ApertureValueToFNumber(-1d + 1d/3)},
            {"0.9", ApertureValueToFNumber(-1d + 2d/3)},
            {"1.0", 1d},
            {"1.1", ApertureValueToFNumber(1d/3)},
            {"1.2", ApertureValueToFNumber(2d/3)},
            {"1.4", ApertureValueToFNumber(1d)},
            {"1.6", ApertureValueToFNumber(1d + 1d/3)},
            {"1.8", ApertureValueToFNumber(1d + 2d/3)},
            {"2",   2d},
            {"2.2", ApertureValueToFNumber(2d + 1d/3)},
            {"2.5", ApertureValueToFNumber(2d + 2d/3)},
            {"2.8", ApertureValueToFNumber(3d)},
            {"3.2", ApertureValueToFNumber(3d + 1d/3)},
            {"3.5", ApertureValueToFNumber(3d + 2d/3)},
            {"4",   4d},
            {"4.5", ApertureValueToFNumber(4d + 1d/3)},
            {"5.0", ApertureValueToFNumber(4d + 2d/3)},
            {"5.6", ApertureValueToFNumber(5d)},
            {"6.3", ApertureValueToFNumber(5d + 1d/3)},
            {"7.1", ApertureValueToFNumber(5d + 2d/3)},
            {"8",   8d},
            {"9",   ApertureValueToFNumber(6d + 1d/3)},
            {"10",  ApertureValueToFNumber(6d + 2d/3)},
            {"11",  ApertureValueToFNumber(7d)},
            {"13",  ApertureValueToFNumber(7d + 1d/3)},
            {"14",  ApertureValueToFNumber(7d + 2d/3)},
            {"16",  16d},
            {"18",  ApertureValueToFNumber(8d + 1d/3)},
            {"20",  ApertureValueToFNumber(8d + 2d/3)},
            {"22",  ApertureValueToFNumber(9d)},
            // I don't know what the traditional 1/3 or 1/2 stops beyond f/22 are.
            {"32",  32d},
            {"45",  ApertureValueToFNumber(11d)},
            {"64",  64d},
        };

        private static double ApertureValueToFNumber(double apertureValue)
        {
            return Math.Sqrt(Math.Pow(2, apertureValue));
        }

        private RotateTransform GetBrushRotation()
        {
            var rotation = new RotateTransform();
            rotation.CenterX = 0.5;
            rotation.CenterY = 0.5;

            var orientationBinding = new Binding("Orientation");
            orientationBinding.Source = this;
            BindingOperations.SetBinding(rotation, RotateTransform.AngleProperty, orientationBinding);

            return rotation;
        }

        private static string FStopToString(double value)
        {
            return string.Format("f/{0:G2}", value);
        }

        private static string ExposureTimeToShutterSpeed(UInt32 exposureTimeUSec)
        {
            if (exposureTimeUSec > 333333)
            {
                // Express as seconds.
                return string.Format("{0:G2} s", exposureTimeUSec / 1E6);
            }
            else
            {
                // Express as reciprocal seconds.
                return string.Format("1/{0:G} s", Math.Round(1e6 / exposureTimeUSec, MidpointRounding.AwayFromZero));
            }
        }

        private static double GetAverageLuminosity(BitmapSource source)
        {
            var wbmp = new WriteableBitmap(source);

            double total = 0.0;
            for (int i = 0; i < wbmp.Pixels.Length; i++)
            {
                UInt32 argb = (UInt32)wbmp.Pixels[i];

                // There shouldn't be any alpha transparency here.
                System.Diagnostics.Debug.Assert((argb >> 24) == 0xFF);

                var color = Color.FromArgb(0xFF, (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));
                double value = (double)(color.R + color.G + color.B) / (3 * 0xFF);

                total += value;
            }

            return total / wbmp.Pixels.Length;
        }

        private void RootFrame_OrientationChanged(object sender, OrientationChangedEventArgs e)
        {
            Orientation = GetOrientation();
        }

        private int GetOrientation()
        {
            int encodedOrientation = 0;

            switch (App.RootFrame.Orientation)
            {
                case PageOrientation.LandscapeLeft:
                    encodedOrientation = -90 + m_sensorOrientation;
                    break;
                case PageOrientation.LandscapeRight:
                    encodedOrientation = 90 + m_sensorOrientation;
                    break;
                case PageOrientation.PortraitUp:
                    encodedOrientation = m_sensorOrientation;
                    break;
                case PageOrientation.PortraitDown:
                    encodedOrientation = 180 + m_sensorOrientation;
                    break;
            }

            return encodedOrientation;
        }

        private string DumpProperties(CameraCaptureFrame frame)
        {
            if (m_guids == null)
            {
                m_guids = new Dictionary<Guid, string>();

                var propertyClasses = new List<Type> 
                {
                    typeof(KnownCameraPhotoProperties),
                    typeof(KnownCameraGeneralProperties),
                    typeof(KnownCameraAudioVideoProperties)
                };

                foreach (Type t in propertyClasses)
                {
                    var properties = t.GetProperties(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                        );

                    foreach (PropertyInfo propInfo in properties)
                    {
                        if (propInfo.GetMethod.ReturnType == typeof(Guid))
                        {
                            Guid g = (Guid)propInfo.GetValue(null, null); // Static properties
                            m_guids.Add(g, propInfo.Name);
                        }
                        else
                        {
                            // Non-Guid property?!
                            System.Diagnostics.Debugger.Break();
                        }
                    }
                }
            }

            string text = string.Empty;

            foreach (var prop in frame.AppliedProperties)
            {
                string keyName = prop.Key.ToString();
                if (m_guids.ContainsKey(prop.Key))
                {
                    keyName = m_guids[prop.Key];
                }

                text += keyName + ": " + prop.Value + "\n";
            }

            return text;
        }
        private IDictionary<Guid, string> m_guids;
    }
}