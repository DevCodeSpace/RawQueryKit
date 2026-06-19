using System;
using System.Collections.Generic;

namespace RawQueryKit
{
    public class RawQueryResult
    {
        /// <summary>
        /// Generated SQL condition snippet (e.g. "p.Name = @Filter_Name_0 AND p.Price > @Filter_Price_1").
        /// </summary>
        public string WhereClause { get; set; }

        /// <summary>
        /// Generated SQL sorting snippet (e.g. "p.Name ASC, p.Price DESC").
        /// </summary>
        public string OrderByClause { get; set; }

        /// <summary>
        /// Generated SQL pagination snippet (e.g. "LIMIT @Limit OFFSET @Offset" or "OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY").
        /// </summary>
        public string PaginationClause { get; set; }

        /// <summary>
        /// Parameters to pass to ADO.NET / Dapper command.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Returns the WHERE clause. Optionally adds the 'WHERE' keyword.
        /// </summary>
        public string GetWhereClause(bool includeWhereKeyword = false)
        {
            if (string.IsNullOrWhiteSpace(WhereClause)) return string.Empty;
            return includeWhereKeyword ? $" WHERE {WhereClause}" : WhereClause;
        }

        /// <summary>
        /// Returns the ORDER BY clause. Optionally adds the 'ORDER BY' keyword.
        /// </summary>
        public string GetOrderByClause(bool includeOrderByKeyword = false)
        {
            if (string.IsNullOrWhiteSpace(OrderByClause)) return string.Empty;
            return includeOrderByKeyword ? $" ORDER BY {OrderByClause}" : OrderByClause;
        }

        /// <summary>
        /// Returns the pagination clause, or an empty string if pagination is not enabled/configured.
        /// </summary>
        public string GetPaginationClause()
        {
            return PaginationClause ?? string.Empty;
        }
    }
}
