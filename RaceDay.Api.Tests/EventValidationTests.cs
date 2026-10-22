using System.ComponentModel.DataAnnotations;
using System.Globalization;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests;

public class EventValidationTests
{
    [Theory]
    [InlineData("en-US", "0.01", true)]
    [InlineData("fr-FR", "0.01", true)]
    [InlineData("fr-FR", "10.50", true)]
    [InlineData("fr-FR", "9999.99", true)]
    [InlineData("fr-FR", "0", false)]
    [InlineData("fr-FR", "10000", false)]
    public void Distance_ValidatesConsistentlyAcrossCultures(
        string cultureName,
        string distanceText,
        bool expectedValid)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

            var distance = decimal.Parse(
                distanceText,
                CultureInfo.InvariantCulture);

            var property = typeof(CreateEventRequest)
                .GetProperty(nameof(CreateEventRequest.Distance))!;

            // Fresh attribute instance for each case avoids cached
            // conversions concealing a regional parsing problem.
            var attribute = Assert.Single(
                property.GetCustomAttributes(typeof(RangeAttribute), true)
                    .Cast<RangeAttribute>());

            Assert.True(attribute.ParseLimitsInInvariantCulture);
            Assert.True(attribute.ConvertValueInInvariantCulture);

            Assert.Equal(expectedValid, attribute.IsValid(distance));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}