using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Media.PhoneExtensions;
using Windows.Foundation;
using Windows.Phone.Media.Capture;
using Windows.Phone.Media.Devices;
using Windows.Storage.Streams;
using ExposureMeter.Resources;

namespace ExposureMeter
{
    public partial class MainPage : PhoneApplicationPage
    {
        // TODO: this needs to be detected per-device.
        // It's correct for Lumia 920 and HTC 8X.
        private static readonly double FStop = 2.0;

        // Constructor
        public MainPage()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TestButton.IsEnabled = false;
            Test()
                .ContinueWith(prevTask => Dispatcher.BeginInvoke(() => { TestButton.IsEnabled = true; }));
        }

        private MemoryStream m_stream;

        private int GetOrientation(PhotoCaptureDevice device)
        {
            int encodedOrientation = 0;
            int sensorOrientation = (Int32)device.SensorRotationInDegrees;

            switch (Orientation)
            {
                case PageOrientation.LandscapeLeft:
                    encodedOrientation = -90 + sensorOrientation;
                    break;
                case PageOrientation.LandscapeRight:
                    encodedOrientation = 90 + sensorOrientation;
                    break;
                case PageOrientation.PortraitUp:
                    encodedOrientation = sensorOrientation;
                    break;
                case PageOrientation.PortraitDown:
                    encodedOrientation = 180 + sensorOrientation;
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

        private void SaveImage(Stream stream, string filename)
        {
            stream.Seek(0, SeekOrigin.Begin);
            new MediaLibrary().SavePicture(filename, stream);
        }

        private double GetAverageLuminosity(BitmapSource source)
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

        private async Task Test()
        {
            var resolutions = PhotoCaptureDevice.GetAvailableCaptureResolutions(CameraSensorLocation.Back);
            var maxRes = resolutions.OrderByDescending(size => size.Width * size.Height).First();
            using (PhotoCaptureDevice device = await PhotoCaptureDevice.OpenAsync(CameraSensorLocation.Back, maxRes))
            {

                device.VendorSpecificDataAvailable += new TypedEventHandler<ICameraCaptureDevice, VendorSpecificDataEventArgs>(
                    (sender, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine("{0}: {1}", e.EventId, e.Data);
                    });

                var sequence = device.CreateCaptureSequence(1);
                await device.PrepareCaptureSequenceAsync(sequence);

                var desiredProperties = new Dictionary<Guid, object>
                {
                    { KnownCameraPhotoProperties.Iso,                       100 },
                    //{ KnownCameraPhotoProperties.ExposureTime,              1e6 / 60 /* microseconds */ },
                    { KnownCameraPhotoProperties.FlashMode,                 FlashState.Off },
                    { KnownCameraPhotoProperties.FocusIlluminationMode,     FocusIlluminationMode.Off },
                    { KnownCameraPhotoProperties.LockedAutoFocusParameters, AutoFocusParameters.Exposure },
                    { KnownCameraGeneralProperties.EncodeWithOrientation,   GetOrientation(device) },
                };

                foreach (KeyValuePair<Guid, object> pair in desiredProperties)
                {
                    // Seems like setting per frame doesn't work right. Set on device instead.
                    //sequence.Frames[0].DesiredProperties[pair.Key] = pair.Value;
                    device.SetProperty(pair.Key, pair.Value);
                }

                m_stream = new MemoryStream();
                sequence.Frames[0].CaptureStream = m_stream.AsOutputStream();

                sequence.FrameAcquired += new TypedEventHandler<CameraCaptureSequence, FrameAcquiredEventArgs>(
                    (sender, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine("Frame {0} acquired", e.Index);
                    });
                await sequence.StartCaptureAsync();

                ////////

                CameraCaptureFrame frame = sequence.Frames[0];

                m_stream.Seek(0, SeekOrigin.Begin);
                var img = new BitmapImage();
                img.SetSource(m_stream);

                UInt32 exposureTime = (UInt32)frame.AppliedProperties[KnownCameraPhotoProperties.ExposureTime];
                UInt32 iso = (UInt32)frame.AppliedProperties[KnownCameraPhotoProperties.Iso];
                double lum = GetAverageLuminosity(img);

                string text = string.Format("ISO: {0}\nShutter: {1} sec\nAverage Luminosity: {2:##.###}%", iso, exposureTime / 1e6, lum * 100);

                m_stream = new MemoryStream(); // Reset for the next photo.

                Dispatcher.BeginInvoke(() =>
                {
                    Text.Text = text;
                    PreviewImage.Source = img;
                });

            }
        }
    }
}