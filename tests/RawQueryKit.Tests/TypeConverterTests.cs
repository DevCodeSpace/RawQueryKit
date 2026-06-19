using System;
using Xunit;
using RawQueryKit.Parsing;

namespace RawQueryKit.Tests
{
    public enum TestEnum
    {
        FirstValue = 1,
        SecondValue = 2
    }

    public class TypeConverterTests
    {
        [Fact]
        public void ConvertValue_ShouldConvertStrings()
        {
            var res = TypeConverter.ConvertValue("hello", typeof(string));
            Assert.Equal("hello", res);
        }

        [Fact]
        public void ConvertValue_ShouldConvertInts()
        {
            var res = TypeConverter.ConvertValue("42", typeof(int));
            Assert.Equal(42, res);
        }

        [Fact]
        public void ConvertValue_ShouldConvertNullables()
        {
            var res = TypeConverter.ConvertValue("100", typeof(int?));
            Assert.Equal(100, res);

            var nullRes = TypeConverter.ConvertValue("null", typeof(int?));
            Assert.Null(nullRes);

            var emptyRes = TypeConverter.ConvertValue("", typeof(int?));
            Assert.Null(emptyRes);
        }

        [Fact]
        public void ConvertValue_ShouldConvertBooleans()
        {
            Assert.True((bool)TypeConverter.ConvertValue("true", typeof(bool)));
            Assert.True((bool)TypeConverter.ConvertValue("1", typeof(bool)));
            Assert.False((bool)TypeConverter.ConvertValue("false", typeof(bool)));
            Assert.False((bool)TypeConverter.ConvertValue("0", typeof(bool)));
        }

        [Fact]
        public void ConvertValue_ShouldConvertGuids()
        {
            var guidString = "d010777c-78c7-4ab9-9524-2c6c0989f5bc";
            var res = TypeConverter.ConvertValue(guidString, typeof(Guid));
            Assert.Equal(Guid.Parse(guidString), res);
        }

        [Fact]
        public void ConvertValue_ShouldConvertEnums()
        {
            var res = TypeConverter.ConvertValue("FirstValue", typeof(TestEnum));
            Assert.Equal(TestEnum.FirstValue, res);

            var resInt = TypeConverter.ConvertValue("2", typeof(TestEnum));
            Assert.Equal(TestEnum.SecondValue, resInt);
        }

        [Fact]
        public void ConvertValue_ShouldConvertNullStringToNullForString()
        {
            var res = TypeConverter.ConvertValue("null", typeof(string));
            Assert.Null(res);
        }

        [Fact]
        public void ConvertValue_ShouldConvertNumericTypes()
        {
            Assert.Equal(123.45, (double)TypeConverter.ConvertValue("123.45", typeof(double)));
            Assert.Equal(12.34f, (float)TypeConverter.ConvertValue("12.34", typeof(float)));
            Assert.Equal(9876543210L, (long)TypeConverter.ConvertValue("9876543210", typeof(long)));
            Assert.Equal((short)32000, (short)TypeConverter.ConvertValue("32000", typeof(short)));
        }

        [Fact]
        public void ConvertValue_ShouldConvertDateTimeOffsetAndTimeSpan()
        {
            var dtoStr = "2026-06-12T16:26:36+05:30";
            var expectedDto = DateTimeOffset.Parse(dtoStr);
            Assert.Equal(expectedDto, (DateTimeOffset)TypeConverter.ConvertValue(dtoStr, typeof(DateTimeOffset)));

            var tsStr = "12:30:15";
            var expectedTs = TimeSpan.Parse(tsStr);
            Assert.Equal(expectedTs, (TimeSpan)TypeConverter.ConvertValue(tsStr, typeof(TimeSpan)));
        }
    }
}

