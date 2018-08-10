// Copyright 2018 Louis S.Berman.
//
// This file is part of TumbleDown.
//
// TumbleDown is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation, either version 3 of the License, 
// or (at your option) any later version.
//
// TumbleDown is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the GNU 
// General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with TumbleDown.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;

namespace TumbleDown
{
    public static class MiscExenders
    {
        public static R Funcify<T, R>(this T value, Func<T, R> func) => func(value);

        public static T ToEnum<T>(this string value) where T : Enum =>
            (T)Enum.Parse(typeof(T), value, true);

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> items) =>
            (items == null) || (!items.Any());

        public static T GetAttribute<T>(this Assembly callingAssembly)
            where T : Attribute
        {
            T result = null;

            var configAttributes = Attribute.
                GetCustomAttributes(callingAssembly, typeof(T), false);

            if (!configAttributes.IsNullOrEmpty())
                result = (T)configAttributes[0];

            return result;
        }

        public static bool IsNonEmptyAndTrimmed(this string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !char.IsWhiteSpace(value[0])
                && !char.IsWhiteSpace(value[value.Length - 1]);
        }

        public static bool IsMatch(this string value,
            string pattern, RegexOptions options = RegexOptions.None)
        {
            return new Regex(pattern, options).IsMatch(value);
        }

        public static void EnsurePathExists(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            value = Path.GetFullPath(value);

            var folder = Path.GetDirectoryName(value);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        public static bool IsFolderName(
            this string value, bool mustBeRooted = true)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                new DirectoryInfo(value);

                if (!mustBeRooted)
                    return true;
                else
                    return Path.IsPathRooted(value);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}
