using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WpfBindingSample.Utils
{
    /// <summary>
    /// System.Drawing.Bitmap to System.Windows.Media.Imaging.BitmapSource
    /// </summary>
    [ValueConversion(typeof(Bitmap), typeof(BitmapSource))]
    class BitmapConverter : IValueConverter
    {
        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject([In] IntPtr hObject);

        private static BitmapSource ToBitmapSource(Bitmap source)
        {
            var handle = source.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    handle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(handle);
            }
        }


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Bitmap bitmap)) return null;

            return ToBitmapSource(bitmap);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
