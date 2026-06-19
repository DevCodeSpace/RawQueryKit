using System;
using System.Collections.Concurrent;

namespace RawQueryKit
{
    /// <summary>
    /// A static factory for accessing RawQueryProcessors. 
    /// This caches the processors to ensure optimal performance without requiring Dependency Injection setup.
    /// </summary>
    public static class RawQuery
    {
        private static readonly ConcurrentDictionary<Type, object> _processors = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Gets or creates a cached RawQueryProcessor for the specified type and dialect.
        /// </summary>
        public static RawQueryProcessor<T> For<T>(SqlDialect dialect = SqlDialect.SqlServer)
        {
            // We use the type as the key. If you use multiple dialects for the same model in the same app, 
            // you should use Dependency Injection instead, but 99% of apps only use one dialect.
            return (RawQueryProcessor<T>)_processors.GetOrAdd(typeof(T), _ => new RawQueryProcessor<T>(dialect));
        }
    }
}
