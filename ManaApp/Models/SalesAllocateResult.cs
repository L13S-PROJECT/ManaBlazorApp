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

// STRICT UI DATA EXTENSION
// Add BatchCode to BatchSelection
// Used only for UI data transfer from SalesAllocateDialog to Planning
// Do NOT add logic or methods
// Do NOT rename existing properties
// Do NOT remove sealed
    
    public sealed class BatchSelection
    {
        public int BatchProductId { get; set; }
        public string BatchCode { get; set; } = ""; // JAUNais
        public int FromStock { get; set; }
        public int FromAssembly { get; set; }
    }
}
