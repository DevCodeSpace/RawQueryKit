using System;

namespace RawQueryKit
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class RawQueryFieldAttribute : Attribute
    {
        /// <summary>
        /// The name used in query strings for filtering and sorting (e.g. "name", "price").
        /// If not set, defaults to the property name in lowercase.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The database column name or SQL expression (e.g. "p.Name", "Price", "COALESCE(p.Description, '')").
        /// If not set, defaults to the property name.
        /// </summary>
        public string Column { get; set; }

        /// <summary>
        /// Determines if the field can be filtered. Defaults to true.
        /// </summary>
        public bool CanFilter { get; set; } = true;

        /// <summary>
        /// Determines if the field can be sorted. Defaults to true.
        /// </summary>
        public bool CanSort { get; set; } = true;

        /// <summary>
        /// Determines if this field should be used as the default sort when no sort is requested.
        /// </summary>
        public bool IsDefaultSort { get; set; } = false;

        /// <summary>
        /// Determines if the default sort should be descending.
        /// </summary>
        public bool DefaultSortDescending { get; set; } = false;
    }
}
