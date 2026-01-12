namespace ManaApp.Models
{
    public sealed class SalesAllocateResult
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";

        // kopsummas (rādīsies Sales sheetā)
        public int StockTotal { get; set; }
        public int AssemblyTotal { get; set; }

        // precīzs sadalījums pa batchiem (iekšējai loģikai)
        public List<SalesBatchQty> StockBatches { get; set; } = new();
        public List<SalesBatchQty> AssemblyBatches { get; set; } = new();
    }

    public sealed class SalesBatchQty
    {
        public int BatchProductId { get; set; }
        public string BatchCode { get; set; } = "";
        public int Qty { get; set; }
    }
}
