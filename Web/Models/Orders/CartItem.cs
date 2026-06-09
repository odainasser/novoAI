using System;
using Web.Models.Products;

namespace Web.Models.Orders
{
    public class CartItem
    {
        public ProductDto Product { get; set; } = new();
        public ProductUnitDto? SelectedUnit { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice => DiscountedUnitPrice ?? (SelectedUnit?.SellingPrice ?? 0);
        public decimal OriginalUnitPrice => SelectedUnit?.SellingPrice ?? 0;
        public decimal OriginalTotal => OriginalUnitPrice * Quantity;
        public decimal Total => UnitPrice * Quantity;
        public bool HasDiscount => DiscountedUnitPrice.HasValue && DiscountedUnitPrice.Value < OriginalUnitPrice;
        
        // Promotion info
        public decimal? DiscountedUnitPrice { get; set; }
        public string? PromotionName { get; set; }
    }
}
