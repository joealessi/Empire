using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EmpireGame
{
    public class MessageTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageType messageType)
            {
                return messageType switch
                {
                    MessageType.Success => Brushes.LimeGreen,
                    MessageType.Warning => Brushes.Orange,
                    MessageType.Error => Brushes.Red,
                    MessageType.Combat => Brushes.Yellow,
                    MessageType.Critical => Brushes.Magenta,
                    MessageType.Movement => Brushes.Cyan,
                    MessageType.Production => Brushes.DodgerBlue,
                    MessageType.Info => Brushes.White,
                    _ => Brushes.White
                };
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}