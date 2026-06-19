namespace RawQueryKit
{
    public class RawQueryOptions
    {
        /// <summary>
        /// Sieve-style filter string, e.g. "name==Laptop,price>1000"
        /// </summary>
        public string Filters { get; set; }

        /// <summary>
        /// Sieve-style sort string, e.g. "name,-price"
        /// </summary>
        public string Sorts { get; set; }

        /// <summary>
        /// The current page number (1-indexed).
        /// </summary>
        public int? Page { get; set; }

        /// <summary>
        /// The number of items to return per page.
        /// </summary>
        public int? PageSize { get; set; }

        /// <summary>
        /// The default page size used when PageSize is not specified. Defaults to 10.
        /// </summary>
        public int DefaultPageSize { get; set; } = 10;

        /// <summary>
        /// The maximum page size allowed. PageSize is capped at this value. Defaults to 100.
        /// </summary>
        public int MaxPageSize { get; set; } = 100;
    }
}
