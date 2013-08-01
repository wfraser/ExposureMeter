using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

            var propertyClasses = new List<Type> 
            {
                typeof(KnownCameraPhotoProperties),
                typeof(KnownCameraGeneralProperties),
                typeof(KnownCameraAudioVideoProperties)
            };

            m_guids = new Dictionary<Guid, string>();

            foreach (Type t in propertyClasses)
            {
                var properties = t.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                    );

                foreach (PropertyInfo propInfo in properties)
                {
                    Guid g = (Guid)propInfo.GetValue(null, null); // Static properties
                    m_guids.Add(g, propInfo.Name);
                }
            }
        }

        private IDictionary<Guid, string> m_guids;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Test();
        }

        private MemoryStream m_stream;

        private async Task Test()
        {
            var resolutions = PhotoCaptureDevice.GetAvailableCaptureResolutions(CameraSensorLocation.Back);
            var maxRes = resolutions.OrderByDescending(size => size.Width * size.Height).First();
            var device = await PhotoCaptureDevice.OpenAsync(CameraSensorLocation.Back, maxRes);

            device.VendorSpecificDataAvailable += new TypedEventHandler<ICameraCaptureDevice,VendorSpecificDataEventArgs>(
                (sender, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("{0}: {1}", e.EventId, e.Data);
                });

            var sequence = device.CreateCaptureSequence(1);
            await device.PrepareCaptureSequenceAsync(sequence);

            sequence.Frames[0].DesiredProperties[KnownCameraPhotoProperties.Iso] = 100;
            sequence.Frames[0].DesiredProperties[KnownCameraPhotoProperties.ExposureTime] = 1e6 / 60; // microseconds
            sequence.Frames[0].DesiredProperties[KnownCameraPhotoProperties.FlashMode] = FlashState.Off;
            sequence.Frames[0].DesiredProperties[KnownCameraPhotoProperties.FocusIlluminationMode] = FocusIlluminationMode.Off;
            sequence.Frames[0].DesiredProperties[KnownCameraPhotoProperties.LockedAutoFocusParameters] = AutoFocusParameters.Exposure;

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

            /*
            m_stream.Seek(0, SeekOrigin.Begin);

            byte[] bytes = new byte[m_stream.Length];
            m_stream.Read(bytes, 0, (int)m_stream.Length);
            //TODO: Inspect bytes here, look for F-stop in Exif

            m_stream.Seek(0, SeekOrigin.Begin);
            new MediaLibrary().SavePicture("test.jpg", m_stream);
             */

            Dispatcher.BeginInvoke(() => { Text.Text = text; });
        }
    }
}