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
            m_isFrozen = false;
            m_viewModel = App.ViewModel;

            DataContext = m_viewModel;

            CameraButtons.ShutterKeyPressed += CameraButtons_ShutterKeyPressed;
        }

        private bool m_isFrozen;
        private MainViewModel m_viewModel;

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
                m_viewModel.Camera.StartPreview();
                m_isFrozen = false;
            }
            else
            {
                CaptureButton.IsEnabled = false;
                m_viewModel.Camera.Capture()
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
            m_viewModel.Camera.Initialize().ContinueWith(x => Dispatcher.BeginInvoke(() => 
                {
                    m_viewModel.Camera.StartPreview();
                    CaptureButton.IsEnabled = true;
                }));
        }

        private void PreviewRectangle_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            m_viewModel.Camera.PreviewSize = e.NewSize;
        }
    }
}