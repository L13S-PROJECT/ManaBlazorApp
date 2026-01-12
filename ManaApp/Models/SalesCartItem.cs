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

    public sealed class SalesBatchItem
    {
        public int BatchProductId { get; set; }
        public int Qty { get; set; }
    }
}
