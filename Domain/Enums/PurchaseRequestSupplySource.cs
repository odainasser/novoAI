namespace Domain.Enums;

public enum PurchaseRequestSupplySource
{
    /// <summary>Items are purchased from an external supplier; converts to a Goods Receiving Note.</summary>
    FromSupplier = 1,

    /// <summary>Items are pulled from the central warehouse; converts to a Stock Transfer.</summary>
    FromCentralWarehouse = 2
}
