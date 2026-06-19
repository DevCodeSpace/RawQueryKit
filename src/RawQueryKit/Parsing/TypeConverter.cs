using System;
using System.ComponentModel;

namespace RawQueryKit.Parsing
{
    public static class TypeConverter
    {
        /// <summary>
        /// Converts a string value to the target C# type.
        /// Treats case-insensitive "null" as a null value for nullable types.
        /// </summary>
        public static object ConvertValue(string value, Type targetType)
        {
            if (value != null && value.Equals("null", StringComparison.OrdinalIgnoreCase) && CanBeNull(targetType))
            {
                return null;
            }

            if (targetType == typeof(string))
            {
                return value;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                // Handle nullable types by returning null
                if (CanBeNull(targetType))
                {
                    return null;
                }
                throw new ArgumentException($"Cannot convert empty value to non-nullable type {targetType.Name}.");
            }

            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Handle Enums
            if (underlyingType.IsEnum)
            {
                try
                {
                    return Enum.Parse(underlyingType, value, true);
                }
                catch
                {
                    if (int.TryParse(value, out int intVal))
                    {
                        return Enum.ToObject(underlyingType, intVal);
                    }
                    throw;
                }
            }

            // Handle Guid
            if (underlyingType == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            // Handle Boolean
            if (underlyingType == typeof(bool))
            {
                if (value == "1") return true;
                if (value == "0") return false;
                return bool.Parse(value);
            }

            // Use TypeConverter for other standard types
            var converter = TypeDescriptor.GetConverter(underlyingType);
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromInvariantString(value);
            }

            return Convert.ChangeType(value, underlyingType);
        }

        private static bool CanBeNull(Type type)
        {
            if (!type.IsValueType) return true; // Reference type
            if (Nullable.GetUnderlyingType(type) != null) return true; // Nullable value type
            return false;
        }
    }
}
