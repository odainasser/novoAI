using Infrastructure.Services;
using Xunit;

namespace UnitTests;

/// <summary>
/// Pure-logic tests for the auto-reorder quantity rule used when generating
/// Purchase Request proposals: suggested = max(0, 2 × threshold − available).
/// </summary>
public class PurchaseRequestReorderTests
{
    [Theory]
    [InlineData(10, 3, 17)]   // below threshold → top up to 2× threshold
    [InlineData(10, 10, 10)]  // exactly at threshold
    [InlineData(10, 0, 20)]   // empty stock
    [InlineData(5, 2, 8)]
    public void CalculateReorderQuantity_tops_up_to_twice_the_threshold(int threshold, int available, int expected)
    {
        Assert.Equal(expected, PurchaseRequestService.CalculateReorderQuantity(threshold, available));
    }

    [Theory]
    [InlineData(10, 25)]  // already well above 2× threshold → nothing to order
    [InlineData(10, 20)]  // exactly at 2× threshold
    [InlineData(0, 0)]
    public void CalculateReorderQuantity_never_returns_negative(int threshold, int available)
    {
        Assert.True(PurchaseRequestService.CalculateReorderQuantity(threshold, available) >= 0);
    }
}
