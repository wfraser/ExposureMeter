using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace ExposureMeter
{
    public partial class ModeControl : UserControl
    {
        public enum Mode
        {
            P,  // Program
            Tv, // Shutter Priority
            Av, // Aperture Priority
            M   // Manual
        }

        public static DependencyProperty CurrentModeProperty = DependencyProperty.Register("CurrentMode", typeof(Mode), typeof(ModeControl),
            new PropertyMetadata(Mode.P, ModePropertyChanged));
        public Mode CurrentMode
        {
            get { return (Mode)GetValue(CurrentModeProperty); }
            set { SetValue(CurrentModeProperty, value); }
        }
        private static void ModePropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var modeControl = (ModeControl)sender;
            modeControl.ModeChanged((Mode)e.NewValue);
        }
        private void ModeChanged(Mode newMode)
        {
            foreach (Button button in GetButtons())
            {
                if ((button.Content as string) == newMode.ToString())
                {
                    button.IsEnabled = false;
                }
                else
                {
                    button.IsEnabled = true;
                }
            }
        }

        public ModeControl()
        {
            InitializeComponent();
        }

        IEnumerable<Button> GetButtons()
        {
            for (int i = VisualTreeHelper.GetChildrenCount(LayoutRoot) - 1; i >= 0; i--)
            {
                Button button = VisualTreeHelper.GetChild(LayoutRoot, i) as Button;
                if (button != null)
                    yield return button;
            }
        }

        void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            Button clicked = (Button)sender;
            CurrentMode = (Mode)Enum.Parse(typeof(Mode), clicked.Content as string);
        }
    }
}
