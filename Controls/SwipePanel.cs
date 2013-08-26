using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ExposureMeter
{
    public class SwipePanel : Panel
    {
        // Controls the speed of the animation. It will move this many panel sizes in 1 second.
        private static readonly double PanelsPerSecond = 1.5;

        public static DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(Orientation), typeof(SwipePanel),
            new PropertyMetadata(Orientation.Horizontal, OrientationPropertyChanged));
        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }
        private static void OrientationPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var swipePanel = (SwipePanel)sender;
            swipePanel.OrientationChanged();
        }
        private void OrientationChanged()
        {
            InvalidateMeasure();
            switch (Orientation)
            {
                case System.Windows.Controls.Orientation.Horizontal:
                    Storyboard.SetTargetProperty(m_animation, new PropertyPath(TranslateTransform.XProperty));
                    break;
                case System.Windows.Controls.Orientation.Vertical:
                    Storyboard.SetTargetProperty(m_animation, new PropertyPath(TranslateTransform.YProperty));
                    break;
            }
        }

        public static DependencyProperty ActivePanelIndexProperty = DependencyProperty.Register("ActivePanelIndex", typeof(int), typeof(SwipePanel),
            new PropertyMetadata(0, ActivePanelIndexPropertyChanged));
        public int ActivePanelIndex
        {
            get { return (int)GetValue(ActivePanelIndexProperty); }
            set { SetValue(ActivePanelIndexProperty, value); }
        }
        private static void ActivePanelIndexPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var swipePanel = (SwipePanel)sender;
            swipePanel.ActivePanelIndexChanged();
        }
        private void ActivePanelIndexChanged()
        {
            GoToPanelIndex(ActivePanelIndex);
        }

        public SwipePanel()
        {
            m_panelCount = -1;
            m_panelSize = new Size(double.NaN, double.NaN);

            m_translate = new TranslateTransform();
            RenderTransform = m_translate;

            m_storyboard = new Storyboard();
            m_animation = new DoubleAnimation();
            m_animation.EasingFunction = new CubicEase() { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            m_storyboard.Children.Add(m_animation);
            Storyboard.SetTarget(m_animation, m_translate);

            ManipulationStarted += OnManipulationStarted;
            ManipulationDelta += OnManipulationDelta;
            ManipulationCompleted += OnManipulationCompleted;

            Loaded += OnLoaded;
        }

        private Size m_panelSize;
        private int m_panelCount;
        private TranslateTransform m_translate;
        private Storyboard m_storyboard;
        private DoubleAnimation m_animation;

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            OrientationChanged();

            int startingPanelIndex = ActivePanelIndex;
            if (startingPanelIndex != 0)
            {
                GoToPanelIndex(startingPanelIndex, immediate: true);
            }
        }

        void OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            e.Handled = true;
            StopAnimation();
        }

        void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            e.Handled = true;

            switch (Orientation)
            {
                // Note that scrolling right or down is a negative translation.
                // Add 0.25x to the bounds to allow bumping slightly past the ends.

                case System.Windows.Controls.Orientation.Horizontal:
                    m_translate.X = Bound(
                        m_translate.X + e.DeltaManipulation.Translation.X,
                        -m_panelSize.Width * (m_panelCount - 1 + 0.25),
                        m_panelSize.Width * 0.25);
                    break;

                case System.Windows.Controls.Orientation.Vertical:
                    m_translate.Y = Bound(
                        m_translate.Y + e.DeltaManipulation.Translation.Y,
                        -m_panelSize.Height * (m_panelCount - 1 + 0.25),
                        m_panelSize.Height * 0.25);
                    break;
            }
        }

        void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            e.Handled = true;

            double translation = double.NaN;
            double velocity = double.NaN;
            double panelSize = double.NaN;

            switch (Orientation)
            {
                case System.Windows.Controls.Orientation.Horizontal:
                    translation = e.TotalManipulation.Translation.X;
                    velocity = e.FinalVelocities.LinearVelocity.X;
                    panelSize = m_panelSize.Width;
                    break;
                case System.Windows.Controls.Orientation.Vertical:
                    translation = e.TotalManipulation.Translation.Y;
                    velocity = e.FinalVelocities.LinearVelocity.Y;
                    panelSize = m_panelSize.Height;
                    break;
            }

            if (Math.Abs(translation) > Math.Abs(panelSize / 2) || Math.Abs(velocity) > panelSize / 2)
            {
                // Animate to the next/prev panel.
                int currentActive = ActivePanelIndex;
                if (translation < 0)
                {
                    if (currentActive == m_panelCount - 1)
                    {
                        // Out of bounds, animate back to the current panel.
                        GoToPanelIndex(currentActive);
                    }
                    else
                    {
                        // Next panel.
                        ActivePanelIndex = currentActive + 1;
                    }
                }
                else
                {
                    if (currentActive == 0)
                    {
                        // Out of bounds, animate back to the current panel.
                        GoToPanelIndex(currentActive);
                    }
                    else
                    {
                        // Previous panel.
                        ActivePanelIndex = currentActive - 1;
                    }
                }
            }
            else
            {
                // User didn't swipe far / fast enough.
                // Animate back to the current panel.
                GoToPanelIndex(ActivePanelIndex);
            }
        }

        private void StopAnimation()
        {
            if (m_storyboard.GetCurrentState() != ClockState.Stopped)
            {
                // Grab the current animated value.
                double current = m_animation.To.Value;
                m_storyboard.Stop();

                // Save the animated value to the actual property.
                switch (Orientation)
                {
                    case System.Windows.Controls.Orientation.Horizontal:
                        m_translate.X = current;
                        break;
                    case System.Windows.Controls.Orientation.Vertical:
                        m_translate.Y = current;
                        break;
                }
            }
        }

        private void GoToPanelIndex(int index, bool immediate = false)
        {
            if (double.IsNaN(m_panelSize.Width) || double.IsNaN(m_panelSize.Height))
            {
                // Probably because ActivePanelIndex was set from XAML.
                // OnLoaded will set it once we know our size.
                return;
            }

            StopAnimation();

            double current = double.NaN;
            double target = double.NaN;
            double panelSize = double.NaN;
            switch (Orientation)
            {
                case System.Windows.Controls.Orientation.Horizontal:
                    current = m_translate.X;
                    target = -m_panelSize.Width * index;
                    if (immediate)
                    {
                        m_translate.X = target;
                    }
                    panelSize = m_panelSize.Width;
                    break;
                case System.Windows.Controls.Orientation.Vertical:
                    current = m_translate.Y;
                    target = -m_panelSize.Height * index;
                    if (immediate)
                    {
                        m_translate.Y = target;
                    }
                    panelSize = m_panelSize.Height;
                    break;
            }

            if (!immediate)
            {
                m_animation.From = current;
                m_animation.To = target;
                //m_animation.Duration = new Duration(TimeSpan.FromSeconds(Math.Abs(m_animation.From.Value - m_animation.To.Value) / (panelSize * PanelsPerSecond)));
                m_animation.Duration = new Duration(TimeSpan.FromSeconds(0.5));
                m_storyboard.Begin();
            }
        }

        private static double Bound(double value, double min, double max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size idealPanelSize = new Size(0, 0);

            if (Children != null)
            {
                m_panelCount = Children.Count;
                foreach (UIElement child in Children)
                {
                    child.Measure(availableSize);
                    idealPanelSize.Width = Math.Max(idealPanelSize.Width, child.DesiredSize.Width);
                    idealPanelSize.Height = Math.Max(idealPanelSize.Height, child.DesiredSize.Height);
                }
            }

            if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
            {
                switch (Orientation)
                {
                    case System.Windows.Controls.Orientation.Horizontal:
                        return new Size(idealPanelSize.Width * m_panelCount, idealPanelSize.Height);
                    case System.Windows.Controls.Orientation.Vertical:
                        return new Size(idealPanelSize.Height, idealPanelSize.Width * m_panelCount);
                    default:
                        throw new InvalidOperationException();
                }
            }
            else
            {
                return availableSize;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children == null || Children.Count == 0)
            {
                return finalSize;
            }

            m_panelSize = finalSize;

            for (int i = 0; i < Children.Count; i++)
            {
                UIElement child = Children[i];
                Point childOrigin = new Point(0, 0);
                switch (Orientation)
                {
                    case System.Windows.Controls.Orientation.Horizontal:
                        childOrigin.X = i * finalSize.Width;
                        break;
                    case System.Windows.Controls.Orientation.Vertical:
                        childOrigin.Y = i * finalSize.Height;
                        break;
                }
                child.Arrange(new Rect(childOrigin, finalSize));
            }

            return finalSize;
        }
    }
}
