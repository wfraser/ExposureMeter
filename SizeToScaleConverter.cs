using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExposureMeter
{
    public class SizeToScaleConverter : IValueConverter
    {
        // Takes two sizes, a source and a target, and returns the scale by which the source needs to be transformed to fit entirely inside the target.
        // Specify the source size as the parameter, and the target size as the input value.
        // i.e. {Binding [TargetSize], Converter=[SizeToScaleConverter], ConverterParameter=[SourceSize]}
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var targetSize = value as Size?;
            if (targetSize == null || !targetSize.HasValue)
                throw new ArgumentException("Input value needs to be a Size");

            var sourceSize = parameter as Size?;
            if (sourceSize == null || !sourceSize.HasValue)
                throw new ArgumentException("Parameter needs to be a Size");

            double targetRatio = targetSize.Value.Width / targetSize.Value.Height;
            double sourceRatio = sourceSize.Value.Width / sourceSize.Value.Height;

            double scale;
            if (sourceRatio > targetRatio)
            {
                scale = targetSize.Value.Width / sourceSize.Value.Width;
            }
            else
            {
                scale = targetSize.Value.Height / sourceSize.Value.Height;
            }

            if (double.IsInfinity(scale))
            {
                scale = 0;
            }

            return scale;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
