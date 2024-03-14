namespace superalloc_analysis
{
    public abstract partial class Program
    {

        ///  ----------------------------------------------------------------------------------------------------------
        ///  ----------------------------------------------------------------------------------------------------------
        ///  ----------------------------------------------------------------------------------------------------------
        ///                                               UTILITY FUNCTIONS
        ///  ----------------------------------------------------------------------------------------------------------
        ///  ----------------------------------------------------------------------------------------------------------
        ///  ----------------------------------------------------------------------------------------------------------
        private static ulong Kb(int v) { return (ulong)v * 1024; }
        private static ulong Mb(int v) { return (ulong)v * 1024 * 1024; }
        private static ulong Gb(int v) { return (ulong)v * 1024 * 1024 * 1024; }

        private static ulong CeilPo2(ulong v)
        {
            var w = CountLeadingZeros(v);
            var l = 0x8000000000000000 >> w;
            if (l == v) return v;
            return l << 1;
        }

        public static ulong FloorPo2(ulong v)
        {
            var w = CountLeadingZeros(v);
            var l = 0x8000000000000000 >> w;
            return l == v ? v : l;
        }

        private static int CountLeadingZeros(ulong integer)
        {
            if (integer == 0)
                return 64;

            var count = 0;
            if ((integer & 0xFFFFFFFF00000000UL) == 0)
            {
                count += 32;
                integer <<= 32;
            }
            if ((integer & 0xFFFF000000000000UL) == 0)
            {
                count += 16;
                integer <<= 16;
            }
            if ((integer & 0xFF00000000000000UL) == 0)
            {
                count += 8;
                integer <<= 8;
            }
            if ((integer & 0xF000000000000000UL) == 0)
            {
                count += 4;
                integer <<= 4;
            }
            if ((integer & 0xC000000000000000UL) == 0)
            {
                count += 2;
                integer <<= 2;
            }
            if ((integer & 0x8000000000000000UL) == 0)
            {
                count += 1;
                integer <<= 1;
            }
            if ((integer & 0x8000000000000000UL) == 0)
            {
                count += 1;
            }
            return count;
        }

        public static int CountTrailingZeros(ulong integer)
        {
            var count = 0;
            if ((integer & 0xFFFFFFFF) == 0)
            {
                count += 32;
                integer >>= 32;
            }
            if ((integer & 0x0000FFFF) == 0)
            {
                count += 16;
                integer >>= 16;
            }
            if ((integer & 0x000000FF) == 0)
            {
                count += 8;
                integer >>= 8;
            }
            if ((integer & 0x0000000F) == 0)
            {
                count += 4;
                integer >>= 4;
            }
            if ((integer & 0x00000003) == 0)
            {
                count += 2;
                integer >>= 2;
            }
            if ((integer & 0x00000001) == 0)
            {
                count += 1;
                integer >>= 1;
            }
            if ((integer & 0x00000001) == 1)
            {
                return count;
            }
            return 0;
        }

        public static bool IsPowerOf2(ulong v)
        {
            return (v & (v - 1)) == 0;
        }

        private static ulong AlignTo(ulong v, ulong a)
        {
            return (v + (a - 1)) & ~(a - 1);
        }
        public static ulong AlignTo8(ulong v)
        {
            return AlignTo(v, 8);
        }
        public static ulong AlignTo16(ulong v)
        {
            return AlignTo(v, 16);
        }
        public static ulong AlignTo32(ulong v)
        {
            return AlignTo(v, 32);
        }
    }

    public static class IntExtensions
    {
        public static string ToByteSize(this int size)
        {
            return string.Format(new FileSizeFormatProvider(), "{0:fs}", size);
        }

        public static string ToByteSize(this long size)
        {
            return string.Format(new FileSizeFormatProvider(), "{0:fs}", size);
        }
        public static string ToByteSize(this uint size)
        {
            return string.Format(new FileSizeFormatProvider(), "{0:fs}", size);
        }

        public static string ToByteSize(this ulong size)
        {
            return string.Format(new FileSizeFormatProvider(), "{0:fs}", size);
        }

        public readonly struct FileSize : IFormattable
        {
            private readonly ulong _value;

            private const int DefaultPrecision = 2;

            private static readonly IList<string> Units = new List<string>() { " B", " KB", " MB", " GB", " TB" };

            public FileSize(ulong value)
            {
                _value = value;
            }

            public static explicit operator FileSize(ulong value)
            {
                return new FileSize(value);
            }

            public override string ToString()
            {
                return ToString(null, null);
            }

            public string ToString(string format)
            {
                return ToString(format, null);
            }

            public string ToString(string format, IFormatProvider formatProvider)
            {
                if (string.IsNullOrEmpty(format))
                    return ToString(DefaultPrecision);
                return int.TryParse(format, out var precision) ? ToString(precision) : _value.ToString(format, formatProvider);
            }

            /// <summary>
            /// Formats the FileSize using the given number of decimals.
            /// </summary>
            private string ToString(int precision)
            {
                var pow = Math.Floor((_value > 0 ? Math.Log(_value) : 0) / Math.Log(1024));
                pow = Math.Min(pow, Units.Count - 1);
                var value = _value / Math.Pow(1024, pow);
                var str = value.ToString(pow == 0 ? "F0" : "F" + precision);
                if (str.EndsWith(".00"))
                    str = str[..^3];
                return str + Units[(int)pow];

            }
        }

        private class FileSizeFormatProvider : IFormatProvider, ICustomFormatter
        {
            public object GetFormat(Type formatType)
            {
                return formatType == typeof(ICustomFormatter) ? this : null;
            }

            /// <summary>
            /// Usage Examples:
            ///		Console2.WriteLine(String.Format(new FileSizeFormatProvider(), "File size: {0:fs}", 100));
            /// </summary>

            private const string FileSizeFormat = "fs";
            private const decimal OneKiloByte = 1024M;
            private const decimal OneMegaByte = OneKiloByte * 1024M;
            private const decimal OneGigaByte = OneMegaByte * 1024M;

            public string Format(string format, object arg, IFormatProvider formatProvider)
            {
                if (format == null || !format.StartsWith(FileSizeFormat))
                {
                    return DefaultFormat(format, arg, formatProvider);
                }

                if (arg is string)
                {
                    return DefaultFormat(format, arg, formatProvider);
                }

                decimal size;

                try
                {
                    size = Convert.ToDecimal(arg);
                }
                catch (InvalidCastException)
                {
                    return DefaultFormat(format, arg, formatProvider);
                }

                string suffix;
                switch (size)
                {
                    case >= OneGigaByte:
                        size /= OneGigaByte;
                        suffix = " GB";
                        break;
                    case >= OneMegaByte:
                        size /= OneMegaByte;
                        suffix = " MB";
                        break;
                    case >= OneKiloByte:
                        size /= OneKiloByte;
                        suffix = " kB";
                        break;
                    default:
                        suffix = " B";
                        break;
                }

                var precision = format[2..];
                if (string.IsNullOrEmpty(precision)) precision = "2";
                if (size == decimal.Floor(size))
                    precision = "0";
                format = "{0:N" + precision + "}"+ suffix;
                return string.Format(format, size);
            }

            private static string DefaultFormat(string format, object arg, IFormatProvider formatProvider)
            {
                if (arg is IFormattable formatterArg)
                {
                    return formatterArg.ToString(format, formatProvider);
                }
                return arg.ToString();
            }

        }
    }
}
