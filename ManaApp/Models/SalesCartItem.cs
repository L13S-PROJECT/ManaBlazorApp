namespace ManaApp.Shared
{
    public class SalesCartItem
    {
        public int VersionId { get; set; }

        public string ProductName { get; set; } = "";

        public string? VersionName { get; set; }

        public int Qty { get; set; }

        public int InStock { get; set; }

        // 🔽 JAUNAIS (iekšējai loģikai, UI nerāda)
        public bool IsAssembly { get; set; }   // true = ASSEMBLY, false = STOCK

        public List<SalesBatchItem> Batches { get; set; } = new();
    }

// Add UI-friendly BatchCode to SalesBatchItem.
// This value comes from SalesAllocateDialog API result.
// Used only for displaying batch info in Edit Sales popup.
// No business logic changes.
  
    public class SalesBatchItem
{
    public int BatchProductId { get; set; }
    public string BatchCode { get; set; } = "";
    public int Qty { get; set; }
    public int AvailableQty { get; set; }   // ⬅️ reālais pieejamais apjoms
}

}