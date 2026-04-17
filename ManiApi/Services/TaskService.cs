using ManiApi.Data;

namespace ManiApi.Services
{
    public class TaskService
    {
        private readonly AppDbContext _db;

        public TaskService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<object>> GetTasksForEmployee(int empId)
        {
            Console.WriteLine("TaskService works");
            return new List<object>();
        }
    }
}