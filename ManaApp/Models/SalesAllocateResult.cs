namespace ManaApp.Models
{
    public sealed class SalesAllocateResult
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";

        public int StockTotal { get; set; }
        public int AssemblyTotal { get; set; }

        public List<BatchSelection> BatchSelections { get; set; } = new();
    }

    public sealed class BatchSelection
    {
        public int BatchProductId { get; set; }
        public int FromStock { get; set; }
        public int FromAssembly { get; set; }
    }
}
