using System;
using System.Collections.Generic;
using System.Linq;

namespace RawQueryKit.Parsing
{
    public static class FilterParser
    {
        // Supported operators sorted by length descending to ensure greedy matching
        private static readonly string[] SortedOperators = new[]
        {
            "!@=*", "!_=*", "!*-=*", // Case-insensitive NOT string operators (4 chars)
            "==*", "!=*", "@=*", "_=*", "*-=*", // Case-insensitive string operators (3 chars)
            "!@=", "!_=", "!*-=", "!^=",    // Case-sensitive NOT string operators + NOT IN (3 chars)
            "==", "!=", ">=", "<=", "@=", "_=", "*-=", "^=", // Standard operators + IN (2 chars)
            ">", "<", "="            // Single character fallbacks
        }.OrderByDescending(o => o.Length).ToArray();

        /// <summary>
        /// Parses a query filter string into a list of OR-grouped lists of AND-terms.
        /// e.g. "a==1,b>2|c<3" -> [[a==1, b>2], [c<3]]
        /// </summary>
        public static List<List<ParsedFilterTerm>> Parse(string filters)
        {
            var result = new List<List<ParsedFilterTerm>>();
            if (string.IsNullOrWhiteSpace(filters))
                return result;

            // Split by '|' for OR groups, ignoring escaped '\|'
            var orGroups = SplitWithEscape(filters, '|');
            foreach (var orGroup in orGroups)
            {
                var andTerms = new List<ParsedFilterTerm>();
                
                // Split by ',' for AND terms, ignoring escaped '\,'
                var termStrings = SplitWithEscape(orGroup, ',');
                ParsedFilterTerm currentTerm = null;

                foreach (var termStr in termStrings)
                {
                    var parsed = ParseTerm(termStr);
                    
                    // If we found a valid operator AND the left side looks like a valid field name (no spaces)
                    if (parsed != null && IsValidFieldName(parsed.FieldName))
                    {
                        if (currentTerm != null)
                        {
                            andTerms.Add(currentTerm);
                        }
                        currentTerm = parsed;
                    }
                    else
                    {
                        // It doesn't look like a new filter. 
                        // This happens if the user typed a normal comma inside their string (e.g., "title=Hello, World").
                        // We will automatically restore the comma and merge it into the previous value.
                        if (currentTerm != null)
                        {
                            // If the termStr had a space at the beginning, Split stripped it? No, Split doesn't strip spaces.
                            // But ParseTerm might have unescaped it. 
                            // termStr is the raw unparsed string.
                            currentTerm.Value += "," + UnescapeValue(termStr);
                        }
                    }
                }

                if (currentTerm != null)
                {
                    andTerms.Add(currentTerm);
                }

                if (andTerms.Any())
                {
                    result.Add(andTerms);
                }
            }

            return result;
        }

        private static bool IsValidFieldName(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return false;
            foreach (char c in fieldName)
            {
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }
            return true;
        }

        private static List<string> SplitWithEscape(string input, char separator, char escape = '\\')
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inEscape = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (inEscape)
                {
                    current.Append(c);
                    inEscape = false;
                }
                else if (c == escape)
                {
                    current.Append(c);
                    inEscape = true;
                }
                else if (c == separator)
                {
                    if (current.Length > 0 || result.Count > 0)
                    {
                        result.Add(current.ToString());
                    }
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0 || result.Count > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        private static ParsedFilterTerm ParseTerm(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return null;

            foreach (var op in SortedOperators)
            {
                int opIndex = term.IndexOf(op, StringComparison.Ordinal);
                if (opIndex > 0) // Field name must be at least 1 character
                {
                    string fieldName = term.Substring(0, opIndex).Trim();
                    string valueStr = term.Substring(opIndex + op.Length).Trim();
                    
                    valueStr = UnescapeValue(valueStr);

                    return new ParsedFilterTerm
                    {
                        FieldName = fieldName,
                        Operator = op,
                        Value = valueStr
                    };
                }
            }

            return null;
        }

        private static string UnescapeValue(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains("\\"))
                return value;

            var sb = new System.Text.StringBuilder(value.Length);
            bool inEscape = false;
            foreach (char c in value)
            {
                if (inEscape)
                {
                    if (c == '|' || c == ',' || c == '\\')
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append('\\');
                        sb.Append(c);
                    }
                    inEscape = false;
                }
                else if (c == '\\')
                {
                    inEscape = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (inEscape) sb.Append('\\');
            return sb.ToString();
        }
    }
}
