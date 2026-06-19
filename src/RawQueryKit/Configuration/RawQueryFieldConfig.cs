using System;

namespace RawQueryKit.Configuration
{
    public class RawQueryFieldConfig
    {
        /// <summary>
        /// The name of the property on the C# model.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// The name of the field as parsed from the query string (case-insensitive).
        /// </summary>
        public string QueryName { get; set; }

        /// <summary>
        /// The database column name or SQL expression to replace the property with.
        /// </summary>
        public string Column { get; set; }

        /// <summary>
        /// The type of the C# property, used for type conversion.
        /// </summary>
        public Type PropertyType { get; set; }

        /// <summary>
        /// True if this property can be filtered.
        /// </summary>
        public bool CanFilter { get; set; } = true;

        /// <summary>
        /// True if this property can be sorted.
        /// </summary>
        public bool CanSort { get; set; } = true;

        public bool IsDefaultSort { get; set; } = false;

        public bool DefaultSortDescending { get; set; } = false;
    }
}
