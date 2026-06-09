using System.Text.Json;
using System.Text.Json.Nodes;
using Infrastructure.Services.Assistant;
using Xunit;

namespace UnitTests;

public class SensitiveDataRedactorTests
{
    [Theory]
    [InlineData("customerEmail", true)]
    [InlineData("CustomerPhone", true)]
    [InlineData("mobileNumber", true)]
    [InlineData("contactPerson", true)]
    [InlineData("address", true)]
    [InlineData("passwordHash", true)]
    [InlineData("totalRevenue", false)]
    [InlineData("productNameEn", false)]
    [InlineData("quantity", false)]
    public void IsSensitiveKey_flags_only_sensitive_fragments(string key, bool expected)
    {
        Assert.Equal(expected, SensitiveDataRedactor.IsSensitiveKey(key));
    }

    [Fact]
    public void Redact_scrubs_pii_but_preserves_business_figures()
    {
        var node = JsonNode.Parse("""
        {
            "totalRevenue": 12345.67,
            "customerName": "Acme",
            "customerEmail": "buyer@example.com",
            "customerPhone": "+971500000000",
            "items": [
                { "productNameEn": "Widget", "quantity": 5, "supplierEmail": "s@x.com" }
            ]
        }
        """)!;

        SensitiveDataRedactor.Redact(node);

        Assert.Equal(12345.67m, node["totalRevenue"]!.GetValue<decimal>());
        Assert.Equal("Acme", node["customerName"]!.GetValue<string>());
        Assert.Equal(SensitiveDataRedactor.RedactedPlaceholder, node["customerEmail"]!.GetValue<string>());
        Assert.Equal(SensitiveDataRedactor.RedactedPlaceholder, node["customerPhone"]!.GetValue<string>());

        var firstItem = node["items"]!.AsArray()[0]!;
        Assert.Equal("Widget", firstItem["productNameEn"]!.GetValue<string>());
        Assert.Equal(5, firstItem["quantity"]!.GetValue<int>());
        Assert.Equal(SensitiveDataRedactor.RedactedPlaceholder, firstItem["supplierEmail"]!.GetValue<string>());
    }

    [Fact]
    public void Redact_handles_null_and_empty_gracefully()
    {
        SensitiveDataRedactor.Redact(null);
        var empty = JsonNode.Parse("{}")!;
        SensitiveDataRedactor.Redact(empty);
        Assert.Equal("{}", empty.ToJsonString(new JsonSerializerOptions()));
    }
}
