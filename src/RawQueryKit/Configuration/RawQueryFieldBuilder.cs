using System;

namespace RawQueryKit.Configuration
{
    public class RawQueryFieldBuilder
    {
        private readonly RawQueryFieldConfig _config;

        public RawQueryFieldBuilder(RawQueryFieldConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Sets the name used in query strings for filtering and sorting (e.g. "name", "price").
        /// </summary>
        public RawQueryFieldBuilder HasName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
            
            _config.QueryName = name;
            return this;
        }

        /// <summary>
        /// Sets the database column name or SQL expression (e.g. "p.Name", "Price", "COALESCE(p.Description, '')").
        /// </summary>
        public RawQueryFieldBuilder HasColumn(string column)
        {
            if (string.IsNullOrWhiteSpace(column))
                throw new ArgumentException("Column cannot be null or whitespace.", nameof(column));
            
            _config.Column = column;
            return this;
        }

        /// <summary>
        /// Configures whether the field can be filtered.
        /// </summary>
        public RawQueryFieldBuilder CanFilter(bool canFilter = true)
        {
            _config.CanFilter = canFilter;
            return this;
        }

        /// <summary>
        /// Configures whether the field can be sorted.
        /// </summary>
        public RawQueryFieldBuilder CanSort(bool canSort = true)
        {
            _config.CanSort = canSort;
            return this;
        }

        /// <summary>
        /// Configures this field as the default sort column when no sort is requested.
        /// </summary>
        public RawQueryFieldBuilder IsDefaultSort(bool descending = false)
        {
            _config.IsDefaultSort = true;
            _config.DefaultSortDescending = descending;
            return this;
        }
    }
}
