using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace RawQueryKit.Configuration
{
    public class RawQueryConfiguration<T>
    {
        public Dictionary<string, RawQueryFieldConfig> Fields { get; } = new Dictionary<string, RawQueryFieldConfig>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Selects a property of the model to configure.
        /// </summary>
        public RawQueryFieldBuilder Property<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            if (propertyExpression == null)
                throw new ArgumentNullException(nameof(propertyExpression));

            MemberExpression memberExpression = propertyExpression.Body as MemberExpression;
            if (memberExpression == null && propertyExpression.Body is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }

            if (memberExpression == null)
            {
                throw new ArgumentException("Expression must represent a property.", nameof(propertyExpression));
            }

            PropertyInfo propertyInfo = memberExpression.Member as PropertyInfo;
            if (propertyInfo == null)
            {
                throw new ArgumentException("Expression must represent a property.", nameof(propertyExpression));
            }

            string propertyName = propertyInfo.Name;
            
            if (!Fields.TryGetValue(propertyName, out var config))
            {
                config = new RawQueryFieldConfig
                {
                    PropertyName = propertyName,
                    Column = propertyName,
                    QueryName = propertyName.ToLowerInvariant(),
                    PropertyType = propertyInfo.PropertyType,
                    CanFilter = true,
                    CanSort = true
                };
                Fields[propertyName] = config;
            }

            return new RawQueryFieldBuilder(config);
        }
    }
}
