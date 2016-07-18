﻿using System;
using Windows.UI.Xaml.Data;

namespace FlatNotes.Converters
{
    public sealed class NullableBooleanToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value as bool?) == true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (value as bool?) == true;
        }
    }
}
