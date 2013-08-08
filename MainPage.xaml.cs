using Microsoft.Devices;
using Microsoft.Phone.Controls;
using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ExposureMeter
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();
            m_camera = new Camera();
            m_isFrozen = false;

            CameraButtons.ShutterKeyPressed += CameraButtons_ShutterKeyPressed;
        }

        private bool m_isFrozen;

        public Camera Camera
        {
            get { return m_camera; }
        }
        private Camera m_camera;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MeterPreviewToggle();
        }

        void CameraButtons_ShutterKeyPressed(object sender, EventArgs e)
        {
            MeterPreviewToggle();
        }

        private void MeterPreviewToggle()
        {
            if (m_isFrozen)
            {
                CaptureButton.Content = "Meter";
                m_camera.StartPreview();
                m_isFrozen = false;
            }
            else
            {
                CaptureButton.IsEnabled = false;
                m_camera.Capture()
                    .ContinueWith(prevTask => Dispatcher.BeginInvoke(() =>
                        {
                            CaptureButton.Content = "Unfreeze";
                            CaptureButton.IsEnabled = true;
                            m_isFrozen = true;
                        }));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            m_camera.Initialize().ContinueWith(x => Dispatcher.BeginInvoke(() => 
                {
                    m_camera.StartPreview();
                    CaptureButton.IsEnabled = true;
                }));
        }
    }
}