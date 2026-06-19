using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace RawQueryKit.Dapper
{
    public static class SqlMapperExtensions
    {
        /// <summary>
        /// Executes a paginated, filtered, and sorted query using RawQueryKit and Dapper.
        /// </summary>
        /// <typeparam name="T">The model type to map the results to.</typeparam>
        /// <param name="connection">The database connection.</param>
        /// <param name="baseSql">The base SELECT statement (e.g. "SELECT * FROM Users").</param>
        /// <param name="options">The API query options (filters, sorts, pagination).</param>
        /// <param name="dialect">The SQL dialect of your database.</param>
        /// <returns>A collection of mapped models.</returns>
        public static async Task<IEnumerable<T>> QueryWithRawQueryKitAsync<T>(
            this IDbConnection connection, 
            string baseSql, 
            RawQueryOptions options, 
            SqlDialect dialect = SqlDialect.SqlServer)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(baseSql)) throw new ArgumentException("Base SQL cannot be empty.", nameof(baseSql));

            // 1. Build the clauses and parameters using RawQueryKit
            var result = RawQuery.For<T>(dialect).Build(options);

            // 2. Map RawQueryKit parameters to Dapper DynamicParameters
            var dynamicParams = new DynamicParameters();
            foreach (var p in result.Parameters)
            {
                dynamicParams.Add(p.Key, p.Value);
            }

            // 3. Assemble the final SQL query
            string finalSql = $@"{baseSql}
                {result.GetWhereClause(includeWhereKeyword: true)}
                {result.GetOrderByClause(includeOrderByKeyword: true)}
                {result.GetPaginationClause()}";

            // 4. Execute using Dapper
            return await connection.QueryAsync<T>(finalSql, dynamicParams);
        }
    }
}
