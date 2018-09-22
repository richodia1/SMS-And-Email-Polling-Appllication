using System;
using log4net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;

namespace SMSGATE.src.Common
{
    public static class StringExtension
    {
        public static String InsertDecimal(this String @this, Int32 precision)
        {
            int res;
            if (!int.TryParse(@this, out res)) return @this;
            String padded = @this.PadLeft(precision, '0');
            return padded.Insert(padded.Length - precision, ".");
        }

        // string extension method ToUpperFirstLetter
        public static string ToUpperFirstLetter(this string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;
            // convert to char array of the string
            char[] letters = source.ToCharArray();
            // upper case the first char
            letters[0] = char.ToUpper(letters[0]);
            // return the array made of the new char array
            return new string(letters);
        }

        public static string ToUppercaseWords(this string value)
        {
            char[] array = value.ToCharArray();
            // Handle the first letter in the string.
            if (array.Length >= 1)
            {
                if (char.IsLower(array[0]))
                {
                    array[0] = char.ToUpper(array[0]);
                }
            }
            // Scan through the letters, checking for spaces.
            // ... Uppercase the lowercase letters following spaces.
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i - 1] == ' ')
                {
                    if (char.IsLower(array[i]))
                    {
                        array[i] = char.ToUpper(array[i]);
                    }
                }
            }
            return new string(array);
        }

        public static string Right(this string value, int length)
        {
            return value.Substring(value.Length - length);
        }

        public static string Format234(this string phone)
        {
            phone = phone.Trim();
            string No234 = "";
            No234 = (phone.StartsWith("234")) ? phone : ("234" + ((phone.StartsWith("0")) ? phone.Remove(0, 1) : phone));

            return No234;
        }

        public static string ToNewLine(this string message)
        {
            return message + Environment.NewLine;
        }
    }
}
