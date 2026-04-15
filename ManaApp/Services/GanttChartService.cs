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
                        var workCenterBusy = new Dictionary<int, int>();
                        var wcEmployees = employeesByWorkCenter.ContainsKey(wc.ID)
                            ? employeesByWorkCenter[wc.ID]
                            : new List<EmployeeDto>();

                        return new WorkCenterGroup
                {
                    WorkCenterId = wc.ID,
                    WorkCenterName = wc.WorkCentr_Name,             

                Employees = wcEmployees
                    .Select(e =>
                        {
                            var tasks = (wcTasks ?? new List<GanttRow>())
                                    .Where(t =>
                                        t.Status != 5 &&
                                        (
                                            t.ClaimedBy == e.Id
                                            || t.AssignedTo == e.Id
                                            || (t.AssignedTo == null && t.ClaimedBy == null)
                                        )
                                    )
                                .OrderBy(t => t.AssignedTo == null ? 1 : 0)
                                .ThenBy(t => t.CanStart ? 0 : 1)
                                .ThenByDescending(t => t.TasksPush)
                                .ThenByDescending(t => t.BatchPriority)
                                .ThenByDescending(t => t.TasksPriority)
                                .ThenBy(t => t.IsPriority ? t.Priority : int.MaxValue)
                                .ThenBy(t => !t.IsPriority ? t.NormalOrder : int.MaxValue)
                                .ThenBy(t => t.BatchProductId)
                                .ThenBy(t => t.StepOrder)
                                .ToList();


                       var employeeBusy = new Dictionary<int, int>();
                        
                        var scheduled = new List<GanttRow>();

                        var available = tasks
                            .Select(t => new GanttRow
                            {
                                BatchProductId = t.BatchProductId,
                                StepOrder = t.StepOrder,
                                EstimatedTotalMinutes = t.EstimatedTotalMinutes,
                                ActualMinutes = t.ActualMinutes,
                                AssignedTo = t.AssignedTo,
                                Status = t.Status,
                                TasksPush = t.TasksPush,
                                BatchPriority = t.BatchPriority,
                                TasksPriority = t.TasksPriority,
                                Priority = t.Priority,
                                IsPriority = t.IsPriority,
                                NormalOrder = t.NormalOrder,
                                WorkCenterId = t.WorkCenterId,
                                CanStart = t.CanStart,
                                DisplayColor = t.DisplayColor
                            })
                            .ToList();

                        var lastStepEnd = new Dictionary<(int batchId, int stepOrder), int>();

while (available.Any())
{
    var next = available
        .Where(t => t.CanStart)
        .DefaultIfEmpty(available.First())
        .OrderBy(t => t.AssignedTo == null ? 1 : 0)
        .ThenByDescending(t => t.TasksPush)
        .ThenByDescending(t => t.BatchPriority)
        .ThenByDescending(t => t.TasksPriority)
        .ThenBy(t => t.Priority)
        .First();

    available.Remove(next);

    var empId = next.AssignedTo ?? e.Id;

    if (!employeeBusy.ContainsKey(empId))
        employeeBusy[empId] = 0;

    var prevKey = ((int)next.BatchProductId!, (int)next.StepOrder! - 1);

    var previousStepEnd = lastStepEnd.ContainsKey(prevKey)
        ? lastStepEnd[prevKey]
        : 0;

    if (!workCenterBusy.ContainsKey(wc.ID))
    workCenterBusy[wc.ID] = 0;

int start;

if (next.AssignedTo != null)
{
    start = Math.Max(
        Math.Max(employeeBusy[empId], previousStepEnd),
        workCenterBusy[wc.ID]
    );
}
else
{
    // pelēkie = tikai employee + dependency
    start = Math.Max(employeeBusy[empId], previousStepEnd);
}

    // ⚠️ klonējam tasku, lai katram darbiniekam būtu savs laiks
        var taskInstance = new GanttRow
        {
            BatchProductId = next.BatchProductId,
            StepOrder = next.StepOrder,
            EstimatedTotalMinutes = next.EstimatedTotalMinutes,
            ActualMinutes = next.ActualMinutes,
            AssignedTo = next.AssignedTo,
            ClaimedBy = next.ClaimedBy,
            Status = next.Status,
            TasksPush = next.TasksPush,
            BatchPriority = next.BatchPriority,
            TasksPriority = next.TasksPriority,
            Priority = next.Priority,
            IsPriority = next.IsPriority,
            NormalOrder = next.NormalOrder,
            WorkCenterId = next.WorkCenterId,
            CanStart = next.CanStart,
            DisplayColor = next.DisplayColor
        };
    
    taskInstance.EstimatedStartMinutes = start;

    var duration = Math.Max(0, next.EstimatedTotalMinutes - next.ActualMinutes);
    // pelēkie taski neietekmē employee noslodzi (what-if simulācija)
    var affectsEmployee = next.AssignedTo != null;
    var currentEnd = start + duration;
    var currentKey = ((int)next.BatchProductId!, (int)next.StepOrder!);
    lastStepEnd[currentKey] = currentEnd;

    
    // ❗ tikai ja assigned → bloķē darbinieku
    if (affectsEmployee)
        {
            employeeBusy[empId] = start + duration;
        }

    if (next.AssignedTo != null)
        {
            workCenterBusy[wc.ID] = start + duration;
        }

    scheduled.Add(taskInstance);

    // 👉 pievienojam nākamos stepus
    var nextSteps = data
        .Where(x =>
            x.BatchProductId == next.BatchProductId &&
            x.StepOrder == next.StepOrder + 1)
        .ToList();

    foreach (var step in nextSteps)
    {
        if (!available.Contains(step) && !scheduled.Contains(step))
        {
            available.Add(step);
        }
    }
}
        return new EmployeeGroup
        {
            EmployeeId = e.Id,
            EmployeeName = e.Name,
            Tasks = scheduled
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
