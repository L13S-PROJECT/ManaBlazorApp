namespace ManaApp.Services
{
    public class AppState
    {
        private EmployeeInfo? _currentEmployee;
        public EmployeeInfo? CurrentEmployee
        {
            get => _currentEmployee;
            set
            {
                _currentEmployee = value;
                NotifyStateChanged();
            }
        }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

    }

    public class EmployeeInfo
    {
        public int Id { get; set; }
        public string Role { get; set; } = "";
        public int? WorkcentrTypeId { get; set; }
    }
}
