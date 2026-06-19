using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RawQueryKit.Configuration;
using RawQueryKit.Parsing;

namespace RawQueryKit
{
    public class RawQueryProcessor<T>
    {
        private readonly SqlDialect _dialect;
        private readonly Dictionary<string, RawQueryFieldConfig> _fields = new Dictionary<string, RawQueryFieldConfig>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RawQueryFieldConfig> _queryNameLookup;

        private static readonly HashSet<string> StringOnlyOperators = new HashSet<string>(StringComparer.Ordinal)
        {
            "@=", "!@=", "_=", "!_=", "*-=", "!*-=",
            "@=*", "!@=*", "_=*", "!_=*", "*-=*", "!*-=*",
            "==*", "!=*"
        };

        /// <summary>
        /// Initializes a new instance of the RawQueryProcessor class.
        /// </summary>
        /// <param name="dialect">The target database SQL dialect.</param>
        /// <param name="configure">Optional fluent configuration builder action.</param>
        public RawQueryProcessor(SqlDialect dialect = SqlDialect.SqlServer, Action<RawQueryConfiguration<T>> configure = null)
        {
            _dialect = dialect;

            // 1. Scan attributes on the class T
            ScanAttributes();

            // 2. Apply fluent configuration if provided
            if (configure != null)
            {
                var config = new RawQueryConfiguration<T>();
                
                // Copy already scanned configurations to the builder
                foreach (var kvp in _fields)
                {
                    config.Fields[kvp.Key] = kvp.Value;
                }

                configure(config);

                // Copy configured fields back
                _fields.Clear();
                foreach (var kvp in config.Fields)
                {
                    _fields[kvp.Key] = kvp.Value;
                }
            }

            // 3. Compile lookup dictionary keyed by QueryName
            _queryNameLookup = _fields.Values.ToDictionary(
                f => f.QueryName,
                f => f,
                StringComparer.OrdinalIgnoreCase
            );
        }

        /// <summary>
        /// Builds the WHERE, ORDER BY, and pagination clauses from raw query options.
        /// </summary>
        public RawQueryResult Build(RawQueryOptions options)
        {
            if (options == null)
            {
                options = new RawQueryOptions();
            }

            var result = new RawQueryResult();
            int parameterCounter = 0;

            // 1. Process Filters
            if (!string.IsNullOrWhiteSpace(options.Filters))
            {
                var filterGroups = FilterParser.Parse(options.Filters);
                var groupClauses = new List<string>();

                foreach (var orGroup in filterGroups)
                {
                    var termClauses = new List<string>();
                    foreach (var term in orGroup)
                    {
                        if (_queryNameLookup.TryGetValue(term.FieldName, out var config))
                        {
                            if (!config.CanFilter)
                            {
                                throw new UnauthorizedAccessException($"Filtering is not allowed on field '{term.FieldName}'.");
                            }

                            string paramName = $"@rq_f_{config.PropertyName}_{parameterCounter++}";
                            var sqlFragment = GenerateFilterSql(config, term.Operator, paramName, term.Value, result.Parameters);
                            if (!string.IsNullOrEmpty(sqlFragment))
                            {
                                termClauses.Add(sqlFragment);
                            }
                        }
                    }

                    if (termClauses.Any())
                    {
                        groupClauses.Add($"({string.Join(" AND ", termClauses)})");
                    }
                }

                if (groupClauses.Any())
                {
                    result.WhereClause = string.Join(" OR ", groupClauses);
                }
            }

            // 2. Process Sorts
            if (!string.IsNullOrWhiteSpace(options.Sorts))
            {
                var sortTerms = SortParser.Parse(options.Sorts);
                var orderParts = new List<string>();

                foreach (var term in sortTerms)
                {
                    if (_queryNameLookup.TryGetValue(term.FieldName, out var config))
                    {
                        if (!config.CanSort)
                        {
                            throw new UnauthorizedAccessException($"Sorting is not allowed on field '{term.FieldName}'.");
                        }

                        string direction = term.Descending ? "DESC" : "ASC";
                        orderParts.Add($"{config.Column} {direction}");
                    }
                }

                if (orderParts.Any())
                {
                    result.OrderByClause = string.Join(", ", orderParts);
                }
            }
            else
            {
                var defaultSortFields = _fields.Values.Where(f => f.IsDefaultSort).ToList();
                if (defaultSortFields.Any())
                {
                    var orderParts = new List<string>();
                    foreach (var config in defaultSortFields)
                    {
                        string direction = config.DefaultSortDescending ? "DESC" : "ASC";
                        orderParts.Add($"{config.Column} {direction}");
                    }
                    result.OrderByClause = string.Join(", ", orderParts);
                }
            }

            // 3. Process Pagination
            if (options.Page.HasValue || options.PageSize.HasValue)
            {
                int page = options.Page ?? 1;
                if (page < 1) page = 1;

                int pageSize = options.PageSize ?? options.DefaultPageSize;
                if (pageSize < 1) pageSize = options.DefaultPageSize;
                if (pageSize > options.MaxPageSize) pageSize = options.MaxPageSize;

                int offset = (page - 1) * pageSize;
                int limit = pageSize;

                result.Parameters.Add("@rq_offset", offset);
                result.Parameters.Add("@rq_limit", limit);

                switch (_dialect)
                {
                    case SqlDialect.SqlServer:
                        if (string.IsNullOrWhiteSpace(result.OrderByClause))
                        {
                            // SQL Server requires an ORDER BY to use OFFSET/FETCH.
                            // If none is provided, default to an arbitrary constant sort.
                            result.OrderByClause = "(SELECT NULL)";
                        }
                        result.PaginationClause = "OFFSET @rq_offset ROWS FETCH NEXT @rq_limit ROWS ONLY";
                        break;

                    case SqlDialect.PostgreSql:
                    case SqlDialect.MySql:
                    case SqlDialect.Sqlite:
                    default:
                        result.PaginationClause = "LIMIT @rq_limit OFFSET @rq_offset";
                        break;
                }
            }

            return result;
        }

        private void ScanAttributes()
        {
            var type = typeof(T);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<RawQueryFieldAttribute>();
                if (attr != null)
                {
                    var queryName = attr.Name ?? prop.Name.ToLowerInvariant();
                    var config = new RawQueryFieldConfig
                    {
                        PropertyName = prop.Name,
                        QueryName = queryName,
                        Column = attr.Column ?? prop.Name,
                        PropertyType = prop.PropertyType,
                        CanFilter = attr.CanFilter,
                        CanSort = attr.CanSort,
                        IsDefaultSort = attr.IsDefaultSort,
                        DefaultSortDescending = attr.DefaultSortDescending
                    };
                    _fields[prop.Name] = config;
                }
            }
        }

        private string GenerateFilterSql(RawQueryFieldConfig config, string op, string paramName, string rawValue, Dictionary<string, object> parameters)
        {
            var propertyType = config.PropertyType;

            // 1. Validate operator applicability
            if (StringOnlyOperators.Contains(op) && propertyType != typeof(string))
            {
                throw new ArgumentException($"Operator '{op}' is only applicable to string properties. Property '{config.PropertyName}' is of type '{propertyType.Name}'.");
            }

            // 2. Handle IN / NOT IN operators directly (comma-separated values)
            if (op == "^=" || op == "!^=")
            {
                var splitValues = rawValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var paramNames = new List<string>();
                
                for (int i = 0; i < splitValues.Length; i++)
                {
                    string valStr = splitValues[i].Trim();
                    object val;
                    try
                    {
                        val = TypeConverter.ConvertValue(valStr, propertyType);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Value '{valStr}' in IN clause is not valid for property '{config.PropertyName}' (expected type {propertyType.Name}).", ex);
                    }
                    
                    string inParamName = $"{paramName}_{i}";
                    parameters.Add(inParamName, val);
                    paramNames.Add(inParamName);
                }
                
                if (paramNames.Count == 0) return string.Empty;
                
                string inClause = string.Join(", ", paramNames);
                if (op == "^=")
                    return $"{config.Column} IN ({inClause})";
                else
                    return $"{config.Column} NOT IN ({inClause})";
            }

            // 3. Parse the target value for standard operators
            object convertedValue;
            try
            {
                convertedValue = TypeConverter.ConvertValue(rawValue, propertyType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Value '{rawValue}' is not valid for property '{config.PropertyName}' (expected type {propertyType.Name}).", ex);
            }

            // 3. Handle NULL values
            if (convertedValue == null || convertedValue == DBNull.Value)
            {
                if (op == "==" || op == "=" || op == "==*")
                {
                    return $"{config.Column} IS NULL";
                }
                else if (op == "!=" || op == "!=*")
                {
                    return $"{config.Column} IS NOT NULL";
                }
                else
                {
                    throw new ArgumentException($"Operator '{op}' is not supported for null values on property '{config.PropertyName}'.");
                }
            }

            // 5. Generate SQL and parameter mapping
            switch (op)
            {
                case "==":
                case "=":
                    parameters.Add(paramName, convertedValue);
                    return $"{config.Column} = {paramName}";

                case "!=":
                    parameters.Add(paramName, convertedValue);
                    return $"{config.Column} != {paramName}";

                case ">":
                    parameters.Add(paramName, convertedValue);
                    return $"{config.Column} > {paramName}";

                case "<":
                    parameters.Add(paramName, convertedValue);
                    return $"{config.Column} < {paramName}";

                case ">=":
                    parameters.Add(paramName, convertedValue);
                    return $"{config.Column} >= {paramName}";

                case "<=":
                    parameters.Add(paramName, convertedValue);
                    return $"{config.Column} <= {paramName}";

                case "==*":
                    parameters.Add(paramName, convertedValue);
                    return $"LOWER({config.Column}) = LOWER({paramName})";

                case "!=*":
                    parameters.Add(paramName, convertedValue);
                    return $"LOWER({config.Column}) != LOWER({paramName})";

                case "@=":
                    parameters.Add(paramName, $"%{convertedValue}%");
                    return $"{config.Column} LIKE {paramName}";

                case "!@=":
                    parameters.Add(paramName, $"%{convertedValue}%");
                    return $"{config.Column} NOT LIKE {paramName}";

                case "_=":
                    parameters.Add(paramName, $"{convertedValue}%");
                    return $"{config.Column} LIKE {paramName}";

                case "!_=":
                    parameters.Add(paramName, $"{convertedValue}%");
                    return $"{config.Column} NOT LIKE {paramName}";

                case "*-=":
                    parameters.Add(paramName, $"%{convertedValue}");
                    return $"{config.Column} LIKE {paramName}";

                case "!*-=":
                    parameters.Add(paramName, $"%{convertedValue}");
                    return $"{config.Column} NOT LIKE {paramName}";

                case "@=*":
                    parameters.Add(paramName, $"%{convertedValue}%");
                    return $"LOWER({config.Column}) LIKE LOWER({paramName})";

                case "!@=*":
                    parameters.Add(paramName, $"%{convertedValue}%");
                    return $"LOWER({config.Column}) NOT LIKE LOWER({paramName})";

                case "_=*":
                    parameters.Add(paramName, $"{convertedValue}%");
                    return $"LOWER({config.Column}) LIKE LOWER({paramName})";

                case "!_=*":
                    parameters.Add(paramName, $"{convertedValue}%");
                    return $"LOWER({config.Column}) NOT LIKE LOWER({paramName})";

                case "*-=*":
                    parameters.Add(paramName, $"%{convertedValue}");
                    return $"LOWER({config.Column}) LIKE LOWER({paramName})";

                case "!*-=*":
                    parameters.Add(paramName, $"%{convertedValue}");
                    return $"LOWER({config.Column}) NOT LIKE LOWER({paramName})";

                default:
                    throw new NotSupportedException($"Operator '{op}' is not supported.");
            }
        }
    }
}
