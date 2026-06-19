# RawQueryKit

[![NuGet](https://img.shields.io/nuget/v/RawQueryKit.svg)](https://www.nuget.org/packages/RawQueryKit)
[![NuGet](https://img.shields.io/nuget/v/RawQueryKit.Dapper.svg?label=RawQueryKit.Dapper)](https://www.nuget.org/packages/RawQueryKit.Dapper)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue.svg)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)

**RawQueryKit** is a lightweight, high-performance, and secure C# library that brings Sieve-style filtering, sorting, and pagination to raw SQL, ADO.NET, and Dapper queries — **without requiring Entity Framework**.

It parses URL query strings, validates field-level access permissions, prevents SQL injection via parameterized queries, and outputs SQL clauses tailored to your specific database dialect.

---

## Table of Contents

- [Features](#features)
- [Supported Databases](#supported-databases)
- [Installation](#installation)
- [Quick Start](#quick-start)
  - [1. Decorate Your Model](#1-decorate-your-model)
  - [2. Use in a Controller or Service](#2-use-in-a-controller-or-service)
  - [3. Usage with Dapper](#3-usage-with-dapper-rawquerykitdapper)
  - [4. Fluent Configuration (No Attributes)](#4-fluent-configuration-no-attributes)
- [Querying the API](#querying-the-api)
- [Filter Operators Reference](#filter-operators-reference)
- [API Reference](#api-reference)
  - [RawQueryFieldAttribute](#rawqueryfieldattribute)
  - [RawQueryOptions](#rawqueryoptions)
  - [RawQueryResult](#rawqueryresult)
  - [RawQuery Static Factory](#rawquery-static-factory)
- [Error Handling](#error-handling)
- [License](#license)

---

## Features

- **Attribute & Fluent Configuration** — Mark properties as filterable/sortable using `[RawQueryField]` attributes, or configure them programmatically with a fluent builder API.
- **Zero DI Setup Required** — Use the high-performance `RawQuery.For<T>()` static factory anywhere in your codebase; processors are cached automatically.
- **SQL Injection Prevention** — All filter values are emitted as named parameters (`@rq_f_...`) and never interpolated into the SQL string.
- **AND/OR Precedence Parsing** — Comma-separated terms within a group are ANDed; pipe-separated groups are ORed. Grouping is emitted as bracketed SQL.
- **Rich Filter Operator Set** — 24 operators covering equality, comparison, `IN`/`NOT IN`, `LIKE`-based pattern matching, case-insensitive variants, and `IS NULL` / `IS NOT NULL`.
- **Smart Comma Parsing** — Intelligently distinguishes between commas as AND-separators and commas that are part of a string value (e.g. `name==Hello, World`).
- **Dialect-Aware Pagination** — Emits `OFFSET … FETCH NEXT` for SQL Server and `LIMIT … OFFSET` for PostgreSQL, MySQL, and SQLite.
- **Default Sort Fallbacks** — Configure fallback sort columns for when the client provides no `sorts` parameter.
- **Custom SQL Expressions** — Map a field to any SQL expression, not just a column name (e.g. `COALESCE(p.Description, '')`).

---

## Supported Databases

RawQueryKit is **database-agnostic** at the query-building level. Pass the appropriate `SqlDialect` and it handles the rest:

| Dialect | Enum Value | Pagination Style |
| :--- | :--- | :--- |
| Microsoft SQL Server | `SqlDialect.SqlServer` | `OFFSET x ROWS FETCH NEXT y ROWS ONLY` |
| PostgreSQL | `SqlDialect.PostgreSql` | `LIMIT y OFFSET x` |
| MySQL | `SqlDialect.MySql` | `LIMIT y OFFSET x` |
| SQLite | `SqlDialect.Sqlite` | `LIMIT y OFFSET x` |

> **Note:** SQL Server requires an `ORDER BY` clause when paginating. If no sort is provided (and no default sort is configured), RawQueryKit automatically injects `ORDER BY (SELECT NULL)` to satisfy this constraint.

---

## Installation

### Core Package

```powershell
dotnet add package RawQueryKit
```

### Dapper Integration (optional)

```powershell
dotnet add package RawQueryKit.Dapper
```

---

## Quick Start

### 1. Decorate Your Model

Apply `[RawQueryField]` to any properties you want to expose for filtering or sorting. The `Name` maps to the URL parameter name, and `Column` maps to the actual SQL column or expression.

```csharp
using RawQueryKit;

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

    // IsDefaultSort = true: used when no ?sorts= is provided by the client
    [RawQueryField(Name = "created_at", Column = "p.CreatedAt", CanFilter = false, CanSort = true,
                   IsDefaultSort = true, DefaultSortDescending = true)]
    public DateTime CreatedAt { get; set; }
}
```

### 2. Use in a Controller or Service

`RawQuery.For<T>()` returns a cached processor — no DI registration required.

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using RawQueryKit;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] RawQueryOptions options)
    {
        // 1. Build SQL clauses and parameters
        var result = RawQuery.For<Product>(SqlDialect.Sqlite).Build(options);

        // 2. Map parameters to the driver-specific parameter type
        var sqlParams = result.Parameters
            .Select(p => new SqliteParameter(p.Key, p.Value ?? DBNull.Value))
            .ToArray();

        // 3. Assemble the final SQL
        string sql = $@"
            SELECT *
            FROM Products p
            {result.GetWhereClause(includeWhereKeyword: true)}
            {result.GetOrderByClause(includeOrderByKeyword: true)}
            {result.GetPaginationClause()}
        ";

        // 4. Execute
        var products = await _db.Products.FromSqlRaw(sql, sqlParams).ToListAsync();
        return Ok(products);
    }
}
```

### 3. Usage with Dapper (`RawQueryKit.Dapper`)

The companion package provides a single extension method that handles the entire pipeline — building clauses, mapping parameters to `DynamicParameters`, and appending the SQL — in one call.

```csharp
using RawQueryKit.Dapper;

// In your service or repository:
var products = await dbConnection.QueryWithRawQueryKitAsync<Product>(
    baseSql:  "SELECT * FROM Products p",
    options:  options,
    dialect:  SqlDialect.PostgreSql
);
```

The generated SQL is equivalent to:

```sql
SELECT * FROM Products p
 WHERE (p.Price > @rq_f_Price_0)
 ORDER BY p.CreatedAt DESC
 LIMIT @rq_limit OFFSET @rq_offset
```

### 4. Fluent Configuration (No Attributes)

If you can't or don't want to annotate your model, configure fields via the fluent builder passed to the `RawQueryProcessor<T>` constructor:

```csharp
var processor = new RawQueryProcessor<Product>(SqlDialect.SqlServer, cfg =>
{
    cfg.Property(p => p.Id)
       .HasName("id")
       .HasColumn("p.Id")
       .CanFilter()
       .CanSort();

    cfg.Property(p => p.Name)
       .HasName("name")
       .HasColumn("p.Name")
       .CanFilter()
       .CanSort();

    cfg.Property(p => p.Price)
       .HasName("price")
       .HasColumn("p.Price")
       .CanFilter()
       .CanSort(false); // Sortable is disabled
});

var result = processor.Build(options);
```

> **Tip:** Fluent configuration and `[RawQueryField]` attributes can be combined. Attributes are scanned first, then the fluent builder can override or add fields.

---

## Querying the API

Clients use standard URL query parameters. RawQueryKit reads them from the `RawQueryOptions` object (which ASP.NET Core binds automatically via `[FromQuery]`).

| Goal | Example URL |
| :--- | :--- |
| Filter by a single field | `GET /api/products?filters=price>500` |
| Filter with equality | `GET /api/products?filters=name==Laptop` |
| Case-insensitive match | `GET /api/products?filters=name==*laptop` |
| Contains substring | `GET /api/products?filters=name@=*book` |
| Multiple filters (AND) | `GET /api/products?filters=price>500,name@=*laptop` |
| OR grouping | `GET /api/products?filters=price>500\|name@=*laptop` |
| IN array | `GET /api/products?filters=category_id^=1,2,3` |
| Null check | `GET /api/products?filters=category_id==null` |
| Sort ascending | `GET /api/products?sorts=name` |
| Sort descending | `GET /api/products?sorts=-price` |
| Multi-column sort | `GET /api/products?sorts=-price,name` |
| Pagination | `GET /api/products?page=2&pageSize=20` |
| Combined | `GET /api/products?filters=price>100&sorts=-created_at&page=1&pageSize=25` |

### AND/OR Grouping Logic

Filters follow standard precedence rules: **AND binds tighter than OR**.

- `a,b|c,d` is parsed as `(a AND b) OR (c AND d)`
- Each group becomes a bracketed SQL expression joined by `OR`

---

## Filter Operators Reference

| Operator | SQL Generated | Description |
| :--- | :--- | :--- |
| `==` | `col = @p` | Equals |
| `!=` | `col != @p` | Not Equals |
| `==null` | `col IS NULL` | Is Null |
| `!=null` | `col IS NOT NULL` | Is Not Null |
| `^=` | `col IN (@p0, @p1, ...)` | In Array |
| `!^=` | `col NOT IN (@p0, @p1, ...)` | Not In Array |
| `>` | `col > @p` | Greater Than |
| `<` | `col < @p` | Less Than |
| `>=` | `col >= @p` | Greater Than or Equal |
| `<=` | `col <= @p` | Less Than or Equal |
| `==*` | `LOWER(col) = LOWER(@p)` | Case-insensitive Equals |
| `!=*` | `LOWER(col) != LOWER(@p)` | Case-insensitive Not Equals |
| `@=` | `col LIKE '%val%'` | Case-sensitive Contains |
| `!@=` | `col NOT LIKE '%val%'` | Case-sensitive Not Contains |
| `@=*` | `LOWER(col) LIKE LOWER('%val%')` | Case-insensitive Contains |
| `!@=*` | `LOWER(col) NOT LIKE LOWER('%val%')` | Case-insensitive Not Contains |
| `_=` | `col LIKE 'val%'` | Case-sensitive Starts With |
| `!_=` | `col NOT LIKE 'val%'` | Case-sensitive Not Starts With |
| `_=*` | `LOWER(col) LIKE LOWER('val%')` | Case-insensitive Starts With |
| `!_=*` | `LOWER(col) NOT LIKE LOWER('val%')` | Case-insensitive Not Starts With |
| `*-=` | `col LIKE '%val'` | Case-sensitive Ends With |
| `!*-=` | `col NOT LIKE '%val'` | Case-sensitive Not Ends With |
| `*-=*` | `LOWER(col) LIKE LOWER('%val')` | Case-insensitive Ends With |
| `!*-=*` | `LOWER(col) NOT LIKE LOWER('%val')` | Case-insensitive Not Ends With |

> **Note:** String-only operators (`@=`, `_=`, `*-=`, `==*`, `!=*`, and their variants) throw an `ArgumentException` if applied to a non-string property.

---

## API Reference

### `RawQueryFieldAttribute`

Applied to model properties to declare filtering and sorting permissions.

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Name` | `string` | Property name (lowercase) | The query string key used by clients |
| `Column` | `string` | Property name | The SQL column name or expression |
| `CanFilter` | `bool` | `true` | Allow this field in `?filters=` |
| `CanSort` | `bool` | `true` | Allow this field in `?sorts=` |
| `IsDefaultSort` | `bool` | `false` | Use as fallback sort when no `?sorts=` is provided |
| `DefaultSortDescending` | `bool` | `false` | Direction of the default sort |

### `RawQueryOptions`

Bind this from `[FromQuery]` in your controller, or construct it manually in services.

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Filters` | `string` | `null` | Filter expression (e.g. `"price>100,name@=*book"`) |
| `Sorts` | `string` | `null` | Sort expression (e.g. `"-price,name"`) |
| `Page` | `int?` | `null` | 1-indexed page number |
| `PageSize` | `int?` | `null` | Number of records per page |
| `DefaultPageSize` | `int` | `10` | Fallback page size when `PageSize` is not provided |
| `MaxPageSize` | `int` | `100` | Upper cap on `PageSize` |

### `RawQueryResult`

Returned by `RawQueryProcessor<T>.Build(options)`.

| Member | Description |
| :--- | :--- |
| `WhereClause` | Raw SQL condition string (without the `WHERE` keyword) |
| `OrderByClause` | Raw SQL sort string (without `ORDER BY`) |
| `PaginationClause` | Raw SQL pagination string (dialect-specific) |
| `Parameters` | `Dictionary<string, object>` of all named parameters to pass to ADO.NET / Dapper |
| `GetWhereClause(bool)` | Returns `WhereClause`, optionally prefixed with ` WHERE ` |
| `GetOrderByClause(bool)` | Returns `OrderByClause`, optionally prefixed with ` ORDER BY ` |
| `GetPaginationClause()` | Returns `PaginationClause`, or `string.Empty` if pagination was not requested |

### `RawQuery` Static Factory

```csharp
// Returns a cached RawQueryProcessor<T> for the given dialect.
// One processor is cached per type; if your app uses multiple dialects
// for the same model, use DI with RawQueryProcessor<T> directly.
RawQueryProcessor<T> processor = RawQuery.For<T>(SqlDialect dialect = SqlDialect.SqlServer);
```

---

## Error Handling

| Situation | Exception Thrown |
| :--- | :--- |
| Client attempts to filter on a field where `CanFilter = false` | `UnauthorizedAccessException` |
| Client attempts to sort on a field where `CanSort = false` | `UnauthorizedAccessException` |
| A string-only operator (e.g. `@=`) is used on a non-string property | `ArgumentException` |
| A filter value cannot be converted to the property's type | `ArgumentException` |
| An unrecognized filter operator is used | `NotSupportedException` |

---

## License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).
