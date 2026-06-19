using Xunit;
using RawQueryKit.Parsing;

namespace RawQueryKit.Tests
{
    public class FilterParserTests
    {
        [Fact]
        public void Parse_ShouldParseSimpleFilter()
        {
            var res = FilterParser.Parse("name==laptop");
            Assert.Single(res);
            Assert.Single(res[0]);
            Assert.Equal("name", res[0][0].FieldName);
            Assert.Equal("==", res[0][0].Operator);
            Assert.Equal("laptop", res[0][0].Value);
        }

        [Fact]
        public void Parse_ShouldParseMultipleAndFilters()
        {
            var res = FilterParser.Parse("name==laptop,price>1000");
            Assert.Single(res);
            Assert.Equal(2, res[0].Count);
            Assert.Equal("name", res[0][0].FieldName);
            Assert.Equal("==", res[0][0].Operator);
            Assert.Equal("laptop", res[0][0].Value);

            Assert.Equal("price", res[0][1].FieldName);
            Assert.Equal(">", res[0][1].Operator);
            Assert.Equal("1000", res[0][1].Value);
        }

        [Fact]
        public void Parse_ShouldParseOrFilters()
        {
            var res = FilterParser.Parse("name==laptop|price>1000");
            Assert.Equal(2, res.Count);
            Assert.Single(res[0]);
            Assert.Single(res[1]);

            Assert.Equal("name", res[0][0].FieldName);
            Assert.Equal("==", res[0][0].Operator);
            Assert.Equal("laptop", res[0][0].Value);

            Assert.Equal("price", res[1][0].FieldName);
            Assert.Equal(">", res[1][0].Operator);
            Assert.Equal("1000", res[1][0].Value);
        }

        [Fact]
        public void Parse_ShouldParseCompoundFilters()
        {
            // (name == laptop AND price > 1000) OR (name == phone AND price > 500)
            var res = FilterParser.Parse("name==laptop,price>1000|name==phone,price>500");
            Assert.Equal(2, res.Count);
            Assert.Equal(2, res[0].Count);
            Assert.Equal(2, res[1].Count);

            Assert.Equal("laptop", res[0][0].Value);
            Assert.Equal("1000", res[0][1].Value);

            Assert.Equal("phone", res[1][0].Value);
            Assert.Equal("500", res[1][1].Value);
        }

        [Fact]
        public void Parse_ShouldParseLongOperatorsGreedily()
        {
            var res = FilterParser.Parse("name@=*laptop");
            Assert.Single(res);
            Assert.Single(res[0]);
            Assert.Equal("name", res[0][0].FieldName);
            Assert.Equal("@=*", res[0][0].Operator);
            Assert.Equal("laptop", res[0][0].Value);
        }
    }
}
