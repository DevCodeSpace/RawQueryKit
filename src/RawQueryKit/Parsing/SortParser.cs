using System;
using System.Collections.Generic;

namespace RawQueryKit.Parsing
{
    public static class SortParser
    {
        /// <summary>
        /// Parses a query sort string into a list of parsed sort terms.
        /// e.g. "name,-price" -> [name ASC, price DESC]
        /// </summary>
        public static List<ParsedSortTerm> Parse(string sorts)
        {
            var result = new List<ParsedSortTerm>();
            if (string.IsNullOrWhiteSpace(sorts))
                return result;

            var terms = sorts.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in terms)
            {
                var trimmed = term.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                bool descending = false;
                string fieldName = trimmed;

                if (trimmed.StartsWith("-"))
                {
                    descending = true;
                    fieldName = trimmed.Substring(1).Trim();
                }
                else if (trimmed.StartsWith("+"))
                {
                    fieldName = trimmed.Substring(1).Trim();
                }

                if (!string.IsNullOrEmpty(fieldName))
                {
                    result.Add(new ParsedSortTerm
                    {
                        FieldName = fieldName,
                        Descending = descending
                    });
                }
            }

            return result;
        }
    }
}
