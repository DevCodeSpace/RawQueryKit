using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using RawQueryKit;

namespace RawQueryKit.Tests
{
    public class Product
    {
        [RawQueryField(Name = "id", Column = "p.Id", CanFilter = true, CanSort = true)]
        public Guid Id { get; set; }

        [RawQueryField(Name = "name", Column = "p.Name", CanFilter = true, CanSort = true)]
        public string Name { get; set; } = null!;

        [RawQueryField(Name = "price", Column = "p.Price", CanFilter = true, CanSort = true)]
        public decimal Price { get; set; }

        [RawQueryField(Name = "category_id", Column = "p.CategoryId", CanFilter = true, CanSort = false)]
        public int? CategoryId { get; set; }

        [RawQueryField(Name = "created_at", Column = "p.CreatedAt", CanFilter = false, CanSort = true, IsDefaultSort = true, DefaultSortDescending = true)]
        public DateTime CreatedAt { get; set; }
    }

    public class FluentProduct
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public double Rating { get; set; }
    }

    public class ProcessorTests
    {
        private readonly ITestOutputHelper _output;

        public ProcessorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AttributeConfiguration_ShouldGenerateCorrectSqlAndParameters()
        {
            var processor = new RawQueryProcessor<Product>(SqlDialect.SqlServer);
            var options = new RawQueryOptions
            {
                Filters = "name==laptop,price>1000",
                Sorts = "price,-name",
                Page = 2,
                PageSize = 10
            };

            var result = processor.Build(options);

            // Verify WHERE clause
            Assert.Contains("p.Name = @rq_f_Name_", result.WhereClause);
            Assert.Contains("p.Price > @rq_f_Price_", result.WhereClause);
            Assert.Contains(" AND ", result.WhereClause);

            // Verify Parameters
            var nameParamKey = GetParamKey(result.Parameters, "Name");
            var priceParamKey = GetParamKey(result.Parameters, "Price");

            Assert.Equal("laptop", result.Parameters[nameParamKey]);
            Assert.Equal(1000m, result.Parameters[priceParamKey]);

            // Verify Sorting
            Assert.Equal("p.Price ASC, p.Name DESC", result.OrderByClause);

            // Verify Pagination (SQL Server specific)
            Assert.Equal("OFFSET @rq_offset ROWS FETCH NEXT @rq_limit ROWS ONLY", result.PaginationClause);
            Assert.Equal(10, result.Parameters["@rq_offset"]);
            Assert.Equal(10, result.Parameters["@rq_limit"]);
        }

        [Fact]
        public void FluentConfiguration_ShouldGenerateCorrectSqlAndParameters()
        {
            var processor = new RawQueryProcessor<FluentProduct>(SqlDialect.PostgreSql, cfg =>
            {
                cfg.Property(p => p.Id).HasName("id").HasColumn("fp.id").CanFilter().CanSort();
                cfg.Property(p => p.Title).HasName("title").HasColumn("fp.title").CanFilter().CanSort();
                cfg.Property(p => p.Rating).HasName("rating").HasColumn("fp.rating").CanFilter(false).CanSort();
            });

            var options = new RawQueryOptions
            {
                Filters = "title@=*awesome",
                Sorts = "rating",
                Page = 1,
                PageSize = 5
            };

            var result = processor.Build(options);

            Assert.Contains("LOWER(fp.title) LIKE LOWER(@rq_f_Title_", result.WhereClause);
            var titleKey = GetParamKey(result.Parameters, "Title");
            Assert.Equal("%awesome%", result.Parameters[titleKey]);

            Assert.Equal("fp.rating ASC", result.OrderByClause);
            
            // PostgreSQL specific pagination
            Assert.Equal("LIMIT @rq_limit OFFSET @rq_offset", result.PaginationClause);
            Assert.Equal(0, result.Parameters["@rq_offset"]);
            Assert.Equal(5, result.Parameters["@rq_limit"]);
        }

        [Fact]
        public void SqlServer_ShouldDefaultToSelectNull_WhenSortingIsMissingForPagination_AndNoDefaultSort()
        {
            var processor = new RawQueryProcessor<FluentProduct>(SqlDialect.SqlServer);
            var options = new RawQueryOptions
            {
                Page = 1,
                PageSize = 10
            };

            var result = processor.Build(options);

            Assert.Equal("(SELECT NULL)", result.OrderByClause);
            Assert.Equal("OFFSET @rq_offset ROWS FETCH NEXT @rq_limit ROWS ONLY", result.PaginationClause);
        }

        [Fact]
        public void Processor_ShouldUseDefaultSort_WhenNoSortIsProvided()
        {
            var processor = new RawQueryProcessor<Product>(SqlDialect.SqlServer);
            var options = new RawQueryOptions
            {
                Page = 1,
                PageSize = 10
            };

            var result = processor.Build(options);

            // Product has IsDefaultSort on CreatedAt with DefaultSortDescending = true
            Assert.Equal("p.CreatedAt DESC", result.OrderByClause);
        }

        [Fact]
        public void InOperator_ShouldGenerateCorrectSqlAndParameters()
        {
            var processor = new RawQueryProcessor<Product>(SqlDialect.SqlServer);
            var options = new RawQueryOptions
            {
                Filters = "category_id^=1,2,3"
            };

            var result = processor.Build(options);

            Assert.Equal($"(p.CategoryId IN (@rq_f_CategoryId_0_0, @rq_f_CategoryId_0_1, @rq_f_CategoryId_0_2))", result.WhereClause);
            Assert.Equal(1, result.Parameters["@rq_f_CategoryId_0_0"]);
            Assert.Equal(2, result.Parameters["@rq_f_CategoryId_0_1"]);
            Assert.Equal(3, result.Parameters["@rq_f_CategoryId_0_2"]);
        }

        [Fact]
        public void NotInOperator_ShouldGenerateCorrectSqlAndParameters()
        {
            var processor = new RawQueryProcessor<Product>(SqlDialect.SqlServer);
            var options = new RawQueryOptions
            {
                Filters = "name!^=Laptop,Phone"
            };

            var result = processor.Build(options);

            Assert.Equal($"(p.Name NOT IN (@rq_f_Name_0_0, @rq_f_Name_0_1))", result.WhereClause);
            Assert.Equal("Laptop", result.Parameters["@rq_f_Name_0_0"]);
            Assert.Equal("Phone", result.Parameters["@rq_f_Name_0_1"]);
        }

        [Fact]
        public void PostgreSql_ShouldNotDefaultToSelectNull_WhenSortingIsMissingForPagination()
        {
            var processor = new RawQueryProcessor<FluentProduct>(SqlDialect.PostgreSql);
            var options = new RawQueryOptions
            {
                Page = 1,
                PageSize = 10
            };

            var result = processor.Build(options);

            Assert.Null(result.OrderByClause);
            Assert.Equal("LIMIT @rq_limit OFFSET @rq_offset", result.PaginationClause);
        }

        [Fact]
        public void Filtering_ShouldThrowUnauthorizedAccess_WhenCanFilterIsFalse()
        {
            var processor = new RawQueryProcessor<Product>();
            var options = new RawQueryOptions { Filters = "created_at>2026-01-01" };

            Assert.Throws<UnauthorizedAccessException>(() => processor.Build(options));
        }

        [Fact]
        public void Sorting_ShouldThrowUnauthorizedAccess_WhenCanSortIsFalse()
        {
            var processor = new RawQueryProcessor<Product>();
            var options = new RawQueryOptions { Sorts = "category_id" };

            Assert.Throws<UnauthorizedAccessException>(() => processor.Build(options));
        }

        [Fact]
        public void StringOperators_ShouldThrowArgumentException_OnNonStringProperty()
        {
            var processor = new RawQueryProcessor<Product>();
            var options = new RawQueryOptions { Filters = "price@=100" }; // Contans operator on decimal

            Assert.Throws<ArgumentException>(() => processor.Build(options));
        }

        [Fact]
        public void NullFilters_ShouldGenerateIsNullAndIsNotNull()
        {
            var processor = new RawQueryProcessor<Product>();
            
            // Equals null
            var options1 = new RawQueryOptions { Filters = "category_id==null" };
            var result1 = processor.Build(options1);
            Assert.Equal("(p.CategoryId IS NULL)", result1.WhereClause);
            Assert.Empty(result1.Parameters);

            // Not equals null
            var options2 = new RawQueryOptions { Filters = "category_id!=null" };
            var result2 = processor.Build(options2);
            Assert.Equal("(p.CategoryId IS NOT NULL)", result2.WhereClause);
            Assert.Empty(result2.Parameters);
        }

        [Fact]
        public void SqliteAndMySql_ShouldGenerateCorrectPagination()
        {
            var processorSqlite = new RawQueryProcessor<Product>(SqlDialect.Sqlite);
            var options = new RawQueryOptions { Page = 3, PageSize = 15 };
            var resultSqlite = processorSqlite.Build(options);

            Assert.Equal("LIMIT @rq_limit OFFSET @rq_offset", resultSqlite.PaginationClause);
            Assert.Equal(30, resultSqlite.Parameters["@rq_offset"]);
            Assert.Equal(15, resultSqlite.Parameters["@rq_limit"]);

            var processorMySql = new RawQueryProcessor<Product>(SqlDialect.MySql);
            var resultMySql = processorMySql.Build(options);
            Assert.Equal("LIMIT @rq_limit OFFSET @rq_offset", resultMySql.PaginationClause);
        }

        [Fact]
        public void FilterOperators_ShouldGenerateCorrectSql_ForStringOperators()
        {
            var processor = new RawQueryProcessor<Product>();

            // Case-insensitive Equals: ==*
            var res1 = processor.Build(new RawQueryOptions { Filters = "name==*Laptop" });
            Assert.Contains("LOWER(p.Name) = LOWER(@rq_f_Name_", res1.WhereClause);
            Assert.Equal("Laptop", res1.Parameters[GetParamKey(res1.Parameters, "Name")]);

            // Case-insensitive Not Equals: !=*
            var res2 = processor.Build(new RawQueryOptions { Filters = "name!=*Laptop" });
            Assert.Contains("LOWER(p.Name) != LOWER(@rq_f_Name_", res2.WhereClause);

            // Case-sensitive Contains: @=
            var res3 = processor.Build(new RawQueryOptions { Filters = "name@=Laptop" });
            Assert.Contains("p.Name LIKE @rq_f_Name_", res3.WhereClause);
            Assert.Equal("%Laptop%", res3.Parameters[GetParamKey(res3.Parameters, "Name")]);

            // Case-sensitive Not Contains: !@=
            var res4 = processor.Build(new RawQueryOptions { Filters = "name!@=Laptop" });
            Assert.Contains("p.Name NOT LIKE @rq_f_Name_", res4.WhereClause);

            // Case-sensitive Starts With: _=
            var res5 = processor.Build(new RawQueryOptions { Filters = "name_=Laptop" });
            Assert.Contains("p.Name LIKE @rq_f_Name_", res5.WhereClause);
            Assert.Equal("Laptop%", res5.Parameters[GetParamKey(res5.Parameters, "Name")]);

            // Case-sensitive Not Starts With: !_=
            var res6 = processor.Build(new RawQueryOptions { Filters = "name!_=Laptop" });
            Assert.Contains("p.Name NOT LIKE @rq_f_Name_", res6.WhereClause);

            // Case-sensitive Ends With: *-=
            var res7 = processor.Build(new RawQueryOptions { Filters = "name*-=Laptop" });
            Assert.Contains("p.Name LIKE @rq_f_Name_", res7.WhereClause);
            Assert.Equal("%Laptop", res7.Parameters[GetParamKey(res7.Parameters, "Name")]);

            // Case-sensitive Not Ends With: !*-=
            var res8 = processor.Build(new RawQueryOptions { Filters = "name!*-=Laptop" });
            Assert.Contains("p.Name NOT LIKE @rq_f_Name_", res8.WhereClause);

            // Case-insensitive Contains: @=*
            var res9 = processor.Build(new RawQueryOptions { Filters = "name@=*Laptop" });
            Assert.Contains("LOWER(p.Name) LIKE LOWER(@rq_f_Name_", res9.WhereClause);

            // Case-insensitive Not Contains: !@=*
            var res10 = processor.Build(new RawQueryOptions { Filters = "name!@=*Laptop" });
            Assert.Contains("LOWER(p.Name) NOT LIKE LOWER(@rq_f_Name_", res10.WhereClause);

            // Case-insensitive Starts With: _=*
            var res11 = processor.Build(new RawQueryOptions { Filters = "name_=*Laptop" });
            Assert.Contains("LOWER(p.Name) LIKE LOWER(@rq_f_Name_", res11.WhereClause);

            // Case-insensitive Not Starts With: !_=*
            var res12 = processor.Build(new RawQueryOptions { Filters = "name!_=*Laptop" });
            Assert.Contains("LOWER(p.Name) NOT LIKE LOWER(@rq_f_Name_", res12.WhereClause);

            // Case-insensitive Ends With: *-=*
            var res13 = processor.Build(new RawQueryOptions { Filters = "name*-=*Laptop" });
            Assert.Contains("LOWER(p.Name) LIKE LOWER(@rq_f_Name_", res13.WhereClause);

            // Case-insensitive Not Ends With: !*-=*
            var res14 = processor.Build(new RawQueryOptions { Filters = "name!*-=*Laptop" });
            Assert.Contains("LOWER(p.Name) NOT LIKE LOWER(@rq_f_Name_", res14.WhereClause);
        }

        [Fact]
        public void RawQueryResult_Helpers_ShouldReturnFormattedClauses()
        {
            var resultEmpty = new RawQueryResult();
            Assert.Equal(string.Empty, resultEmpty.GetWhereClause(true));
            Assert.Equal(string.Empty, resultEmpty.GetWhereClause(false));
            Assert.Equal(string.Empty, resultEmpty.GetOrderByClause(true));
            Assert.Equal(string.Empty, resultEmpty.GetOrderByClause(false));
            Assert.Equal(string.Empty, resultEmpty.GetPaginationClause());

            var resultFilled = new RawQueryResult
            {
                WhereClause = "p.Id = @p0",
                OrderByClause = "p.Name ASC",
                PaginationClause = "LIMIT 10"
            };
            Assert.Equal(" WHERE p.Id = @p0", resultFilled.GetWhereClause(true));
            Assert.Equal("p.Id = @p0", resultFilled.GetWhereClause(false));
            Assert.Equal(" ORDER BY p.Name ASC", resultFilled.GetOrderByClause(true));
            Assert.Equal("p.Name ASC", resultFilled.GetOrderByClause(false));
            Assert.Equal("LIMIT 10", resultFilled.GetPaginationClause());
        }

        [Fact]
        public void ManualVerification_PrintOutput()
        {
            var processorSql = new RawQueryProcessor<Product>(SqlDialect.SqlServer);
            var options = new RawQueryOptions
            {
                Filters = "name@=*laptop,price>1000|category_id==null",
                Sorts = "price,-name",
                Page = 2,
                PageSize = 10
            };
            var resSql = processorSql.Build(options);

            _output.WriteLine("=== SQL SERVER GENERATION ===");
            _output.WriteLine($"WHERE: {resSql.GetWhereClause(true)}");
            _output.WriteLine($"ORDER BY: {resSql.GetOrderByClause(true)}");
            _output.WriteLine($"PAGINATION: {resSql.GetPaginationClause()}");
            _output.WriteLine("PARAMETERS:");
            foreach (var kv in resSql.Parameters)
            {
                _output.WriteLine($"  {kv.Key} = {kv.Value} ({kv.Value?.GetType().Name})");
            }

            var processorSqlite = new RawQueryProcessor<Product>(SqlDialect.Sqlite);
            var resSqlite = processorSqlite.Build(options);
            _output.WriteLine("\n=== SQLITE GENERATION ===");
            _output.WriteLine($"WHERE: {resSqlite.GetWhereClause(true)}");
            _output.WriteLine($"ORDER BY: {resSqlite.GetOrderByClause(true)}");
            _output.WriteLine($"PAGINATION: {resSqlite.GetPaginationClause()}");
        }

        private string GetParamKey(Dictionary<string, object> parameters, string propertyName)
        {
            foreach (var key in parameters.Keys)
            {
                if (key.StartsWith($"@rq_f_{propertyName}_"))
                {
                    return key;
                }
            }
            throw new KeyNotFoundException($"Parameter for property {propertyName} was not found.");
        }
    }
}

