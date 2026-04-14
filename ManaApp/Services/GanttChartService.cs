using ManaApp.Models;

namespace ManaApp.Services
{
    public class GanttChartService
    {
        public List<WorkCenterGroup> BuildGroups(
            List<GanttRow> data,
            List<EmployeeDto> employees,
            List<WorkCenterDto> workCenters)
        {
            
            var employeesByWorkCenter = employees
                .GroupBy(e => e.WorkCentrTypeID ?? 0)
                .ToDictionary(g => g.Key, g => g.ToList());
            var test = employeesByWorkCenter.Keys.ToList();

            return workCenters
                //.Where(wc => wc.IsActive == 1 || wc.IsActive == true)
                .OrderBy(wc => wc.WorkCenter_Order)
                .Select(wc =>
                    {
                        var wcTasks = data
                            .Where(t => t.WorkCenterId == wc.ID)
                            .ToList();

                        return new WorkCenterGroup
                {
                    WorkCenterId = wc.ID,
                    WorkCenterName = wc.WorkCentr_Name,             

                Employees = employees
                    .Where(e => e.WorkCentrTypeID == wc.ID)
                    .ToList()
                        
                    .Select(e =>
                        {
                            var tasks = (wcTasks ?? new List<GanttRow>())
                                .Where(t => t.AssignedTo == e.Id)
                                .OrderByDescending(t => t.IsPriority)
                                .ThenBy(t => t.Priority)
                                .ThenBy(t => t.BatchProductId)
                                .ThenBy(t => t.StepOrder)
                                .ToList();

                            var employeeBusy = new Dictionary<int, int>();

                       foreach (var t in tasks)
                            {
                                var empId = e.Id;

                                    if (!employeeBusy.ContainsKey(empId))
                                        employeeBusy[empId] = 0;

                                    var start = employeeBusy[empId];

                                    t.EstimatedStartMinutes = start;

                                    var duration = Math.Max(0, t.EstimatedTotalMinutes - t.ActualMinutes);

                                    employeeBusy[empId] = start + duration;
                            }

                            return new EmployeeGroup
                            {
                                EmployeeId = e.Id,
                                EmployeeName = e.Name,
                                Tasks = tasks
                            };
                        })
                        .ToList()
                };
        })
                .ToList();
        }

        public List<GanttRow> BuildSimulatedTasks(List<GanttRow> data)
                {
                    var ordered = data
                        .OrderByDescending(t => t.IsPriority)
                        .ThenBy(t => t.Priority)
                        .ThenBy(t => t.BatchProductId)
                        .ThenBy(t => t.StepOrder)
                        .ToList();

                    var result = new List<GanttRow>();

                    var batchPointers = ordered
                        .GroupBy(t => t.BatchProductId)
                        .ToDictionary(g => g.Key, g => 0);

                    var stepsByBatch = ordered
                        .GroupBy(t => t.BatchProductId)
                        .ToDictionary(g => g.Key, g => g.OrderBy(x => x.StepOrder).ToList());

                    var available = new List<GanttRow>();

                    // paņemam pirmo step no katra batch
                    foreach (var kv in stepsByBatch)
                    {
                        if (kv.Value.Any())
                            available.Add(kv.Value[0]);
                    }

                    while (available.Any())
                    {
                        var next = available
                            .OrderByDescending(t => t.IsPriority)
                            .ThenBy(t => t.Priority)
                            .ThenBy(t => t.BatchProductId)
                            .First();

                        available.Remove(next);
                        result.Add(next);

                        var lastEnd = result.Count > 1
                            ? result[result.Count - 2].EstimatedStartMinutes
                            : 0;

                        next.EstimatedStartMinutes = lastEnd;

                        var list = stepsByBatch[next.BatchProductId];
                        var index = list.IndexOf(next);

                        if (index + 1 < list.Count)
                        {
                            available.Add(list[index + 1]);
                        }
                    }

                    return result;
                }
    }
}
