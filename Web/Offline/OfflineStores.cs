namespace Web.Offline;

// Names of the IndexedDB object stores defined in /wwwroot/js/cashier-offline.js.
// Centralising them here means a typo can't drift between C# and JS.
public static class OfflineStores
{
    public const string Credential = "credential";
    public const string Profile = "profile";
    public const string Stores = "stores";
    public const string Products = "products";
    public const string Shifts = "shifts";
    public const string Orders = "orders";
    public const string SyncQueue = "sync_queue";

    public static class Indexes
    {
        public const string ProductsByStore = "storeId";
        public const string ShiftsByStore = "storeId";
        public const string OrdersByStore = "storeId";
        public const string OrdersByStoreCreatedAt = "storeId_createdAt";
    }
}
