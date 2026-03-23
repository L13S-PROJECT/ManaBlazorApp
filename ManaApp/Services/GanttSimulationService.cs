// Gantt simulācija - aprēķina detaļas par pasūtījuma izpildi

using ManaApp.Models;
using System.Net.Http.Json;

namespace ManaApp.Services
{
    public class GanttSimulationService
{
    private readonly HttpClient _http;
    private List<CompanyCalendarModel>? _calendarCache;
    private List<EmployeeWorkLogModel>? _workLogCache;
    private List<EmployeeAvailabilityModel>? _availabilityCache;
    public GanttSimulationService(HttpClient http)
    {
        _http = http;
    }
        public async Task<DetailResult> CalculateDetail(
            List<TaskDto> tasks,
            int quantity,
            DateTime? queueStart = null,
            Dictionary<int, DateTime>? sharedEmployeeBusy = null,
            Dictionary<int, List<DateTime>>? sharedWorkCenterBusy = null)
                {
                    var calendar = _calendarCache ??= await GetCompanyCalendar();
                    var workLogs = _workLogCache ??= await GetEmployeeWorkLog(DateTime.Today.AddDays(-7), DateTime.Today.AddDays(30));
                    var availability = _availabilityCache ??= await GetEmployeeAvailability(DateTime.Today.AddDays(-7), DateTime.Today.AddDays(30));
                    var calendarDict = calendar.ToDictionary(c => c.WorkDate.Date);
                    var workLogDict = workLogs
                        .GroupBy(x => (x.EmployeeID, x.WorkDate.Date))
                        .ToDictionary(g => g.Key, g => g.First());
                    var availabilityDict = availability
                        .GroupBy(x => x.EmployeeID)
                        .ToDictionary(g => g.Key, g => g.ToList());
                    var result = new DetailResult
                        {
                            Status = "-"
                        };

                    var detailTasks = tasks.Where(t => t.StepType == 1).ToList();
                    var assemblyTasks = tasks.Where(t => t.StepType == 2).ToList();

                    if (!detailTasks.Any())
                        return result;

                    var statuses = detailTasks.Select(t => t.Status).ToList();

                    //  TAVA loģika
                    if (statuses.All(s => s == 5))
                        result.Status = "Nav iesākts";
                    else if (statuses.All(s => s == 3))
                        result.Status = "Pabeigts";
                    else if (statuses.Any(s => s == 1 || s == 2 || s == 3))
                        result.Status = "Procesā";

                    // 🔥 neiesākti (minūtes → dienas)
                    if (statuses.All(s => s == 5))
                            {
                                var totalMinutes = detailTasks.Sum(t => t.EstimatedTotalMinutes);
                                result.NotStartedDays = totalMinutes / 60.0 / 8.0;
                            }
                        else
                            {
                                var notStarted = detailTasks.Where(t => t.Status == 5);
                                var totalMinutes = notStarted.Sum(t => t.EstimatedTotalMinutes);
                                result.NotStartedDays = totalMinutes / 60.0 / 8.0;
                            }
                        if (result.NotStartedDays.HasValue)
                            {
                                var totalMin = result.NotStartedDays.Value * 8 * 60;

                                var d = (int)(totalMin / (8 * 60));
                                var h = (int)((totalMin % (8 * 60)) / 60);
                                var m = (int)(totalMin % 60);

                                result.NotStartedText = (d == 0 && h == 0 && m == 0)
                                    ? "-"
                                    : $"{d}d {h}h {m}m";
                            }
                        
                    //  laiks (dienās)

var started = detailTasks
    .Where(t => t.Status != 5 && t.FinishedAt != null)
    .Select(t => t.FinishedAt!.Value)
    .ToList();

var startDate = queueStart ??
    (started.Any()
        ? started.Max()
        : DateTime.Today);

var availableSteps = new List<TaskDto>();

// sākumā – tikai pirmie soļi (StepOrder == 1)
availableSteps.AddRange(
    detailTasks
        .GroupBy(s => s.ProductToPartId)
        .Select(g =>
        {
            // ja ir iesākts → ņem nākamo nepabeigto
            var inProgress = g
                .Where(s => s.Status != 5)
                .OrderByDescending(s => s.StepOrder)
                .FirstOrDefault();

            if (inProgress != null)
            {
                var next = g.FirstOrDefault(s => s.StepOrder == inProgress.StepOrder + 1);
                return next ?? inProgress;
            }

            // ja viss nav iesākts → ņem pirmo
            return g.OrderBy(s => s.StepOrder).First();
        })
        .Where(x => x != null)
        .ToList()
);

var partStepEndTimes = new Dictionary<int, DateTime>();
var partFinishTimes = new Dictionary<int, DateTime>();

var employeeBusy = sharedEmployeeBusy ?? new Dictionary<int, DateTime>();
var workCenterBusy = sharedWorkCenterBusy ?? new Dictionary<int, List<DateTime>>();

while (availableSteps.Any())
{
var step = availableSteps
    .Select(s =>
    {
        DateTime possibleStart = startDate;

        if (partStepEndTimes.ContainsKey(s.ProductToPartId))
            possibleStart = partStepEndTimes[s.ProductToPartId];

        if (s.AssignedTo.HasValue && employeeBusy.ContainsKey(s.AssignedTo.Value))
        {
            var empFree = employeeBusy[s.AssignedTo.Value];
            if (empFree > possibleStart)
                possibleStart = empFree;
        }

        if (s.WorkCenterId.HasValue && workCenterBusy.ContainsKey(s.WorkCenterId.Value))
        {
            var wcFree = workCenterBusy[s.WorkCenterId.Value].Min();
            if (wcFree > possibleStart)
                possibleStart = wcFree;
        }

        return new
        {
            Step = s,
            Start = possibleStart
        };
    })
    .OrderBy(x => x.Start)                //  GALVENAIS
    .ThenByDescending(x => x.Step.TasksPush)
    .ThenByDescending(x => x.Step.PriorityLevel)
    .ThenByDescending(x => x.Step.TasksPriority)
    .ThenBy(x => x.Step.IsPriority ? 0 : 1)
    .ThenBy(x => x.Step.Priority)
    .Select(x => x.Step)
    .First();

    availableSteps.Remove(step);

    // ⏱ start
   DateTime stepStart = partStepEndTimes.ContainsKey(step.ProductToPartId)
    ? partStepEndTimes[step.ProductToPartId]
    : startDate;

    if (partStepEndTimes.ContainsKey(step.ProductToPartId))
        stepStart = partStepEndTimes[step.ProductToPartId];

    // 👷 employee
    if (step.AssignedTo.HasValue)
    {
        var empId = step.AssignedTo.Value;

        if (employeeBusy.ContainsKey(empId))
        {
            var empFreeAt = employeeBusy[empId];
            if (empFreeAt > stepStart)
                stepStart = empFreeAt;
        }
    }

    // 🏭 workcenter
    if (step.WorkCenterId.HasValue)
    {
        var wcId = step.WorkCenterId.Value;

        if (!workCenterBusy.ContainsKey(wcId))
        {
            var capacity = step.Capacity > 0 ? step.Capacity : 1;

            workCenterBusy[wcId] = Enumerable
                .Repeat(stepStart, capacity)
                .ToList();
        }

        var slots = workCenterBusy[wcId];
        var earliest = slots.Min();

        if (earliest > stepStart)
            stepStart = earliest;
    }

    // ⏱ duration
    var stepRemaining = step.EstimatedTotalMinutes - step.ActualMinutes;
    if (stepRemaining < 0) stepRemaining = 0;

    var stepEnd = CalculateStepEnd(stepStart, stepRemaining, calendarDict);

    // 🏭 update WC
    if (step.WorkCenterId.HasValue)
    {
        var slots = workCenterBusy[step.WorkCenterId.Value];
        var index = slots.IndexOf(slots.Min());
        slots[index] = stepEnd;
    }

    // 👷 update employee
    if (step.AssignedTo.HasValue)
    {
        employeeBusy[step.AssignedTo.Value] = stepEnd;
    }

    // 📦 part timing
    partStepEndTimes[step.ProductToPartId] = stepEnd;

    if (step.IsFinal)
    {
        partFinishTimes[step.ProductToPartId] = stepEnd;
    }

    //  unlock next step
var nextStep = detailTasks
    .Where(s => s.ProductToPartId == step.ProductToPartId &&
                s.StepOrder > step.StepOrder)
    .OrderBy(s => s.StepOrder)
    .FirstOrDefault();

    if (nextStep != null)
    {
        availableSteps.Add(nextStep);
    }
}

if (result.Status == "Nav iesākts")
        {
            result.FinishDateText = "-";
        }
else if (result.Status == "Procesā")
{
    if (partFinishTimes.Any())
    {
        var finishDate = partFinishTimes.Values.Max();

        result.FinishDate = finishDate;
        result.FinishDateText = finishDate.ToString("dd.MM.yyyy");
    }
    else
    {
        result.FinishDateText = "-";
    }
}
                    else if (result.Status == "Pabeigts")
                    {
                        var finished = detailTasks
                            .Where(t => t.Status == 3 && t.FinishedAt != null);

                        if (finished.Any())
                        {
                            var lastDate = finished.Max(t => t.FinishedAt);
                                if (lastDate is DateTime dt)
                                    {
                                        result.FinishDate = dt;
                                        result.FinishDateText = dt.ToString("dd.MM.yyyy");
                                    }
                        }
                        else
                        {
                            result.FinishDateText = "-";
                        }
                    }

// =======================
// ASSEMBLY STATUS LOĢIKA
// =======================

if (!assemblyTasks.Any())
{
    result.AssemblyStatus = "-";
}
else
{
    var assemblyStatuses = assemblyTasks.Select(t => t.Status).ToList();

    var detailStatuses = detailTasks.Select(t => t.Status).ToList();

    // 🔴 GAIDA (ja Detail vēl nav gatavs)
    if (detailStatuses.Any(s => s != 3))
    {
        result.AssemblyStatus = "Gaida";
    }
    // 🟡 PROCESĀ
    else if (assemblyStatuses.Any(s => s == 2))
    {
        result.AssemblyStatus = "Procesā";
    }
    // 🟢 PABEIGTS
    else if (assemblyStatuses.All(s => s == 3))
    {
        result.AssemblyStatus = "Pabeigts";
    }
    else
    {
        result.AssemblyStatus = "Gaida";
    }
}

    return result;
 }

private DateTime CalculateStepEnd(
    DateTime stepStart,
    int stepRemaining,
    Dictionary<DateTime, CompanyCalendarModel> calendarDict)
{
    var remainingMinutes = stepRemaining;
    var stepEnd = stepStart;
        if (remainingMinutes <= 0)
        return stepStart;
    var safety = 0;

while (remainingMinutes > 0)
{
    safety++;

if (safety > 365)
    throw new Exception($"Simulation overflow - stepStart={stepStart}, remaining={remainingMinutes}");

    calendarDict.TryGetValue(stepEnd.Date, out var calendarDay);

if (calendarDay == null || !calendarDay.WorkStart.HasValue || !calendarDay.WorkEnd.HasValue)
{
    var fallbackStart = stepEnd.Date.AddHours(8);
    var fallbackEnd = stepEnd.Date.AddHours(17);

    if (stepEnd < fallbackStart)
        stepEnd = fallbackStart;

    var fallbackMinutes = (int)(fallbackEnd - stepEnd).TotalMinutes;

    if (fallbackMinutes <= 0)
    {
        stepEnd = stepEnd.Date.AddDays(1);
        continue;
    }

    if (remainingMinutes <= fallbackMinutes)
    {
        stepEnd = stepEnd.AddMinutes(remainingMinutes);
        remainingMinutes = 0;
    }
    else
    {
        remainingMinutes -= fallbackMinutes;
        stepEnd = stepEnd.Date.AddDays(1);
    }

    continue;
}

        var workStart = stepEnd.Date.Add(calendarDay.WorkStart.Value);
        var workEnd = stepEnd.Date.Add(calendarDay.WorkEnd.Value);

        if (stepEnd < workStart)
        {
            stepEnd = workStart;
        }
        else if (stepEnd >= workEnd)
        {
            stepEnd = stepEnd.Date.AddDays(1);
            continue;
        }

        var availableMinutes = Math.Max(0, (int)(workEnd - stepEnd).TotalMinutes);

        if (calendarDay.Breaks != null && calendarDay.Breaks.Any())
        {
            foreach (var br in calendarDay.Breaks.Where(b => b.IsActive))
            {
                var breakStart = stepEnd.Date.Add(br.BreakStart);
                var breakEnd = stepEnd.Date.Add(br.BreakEnd);

                if (stepEnd >= breakStart && stepEnd < breakEnd)
                {
                    stepEnd = breakEnd;
                }
            }

            availableMinutes = (int)(workEnd - stepEnd).TotalMinutes;
        }

        if (availableMinutes <= 0)
        {
            stepEnd = stepEnd.Date.AddDays(1);
            continue;
        }

        if (remainingMinutes <= availableMinutes)
        {
            stepEnd = stepEnd.AddMinutes(remainingMinutes);
            remainingMinutes = 0;
        }
        else
        {
            remainingMinutes -= availableMinutes;
            stepEnd = stepEnd.Date.AddDays(1);
        }
    }

    return stepEnd;
}

public async Task<List<EmployeeWorkLogModel>> GetEmployeeWorkLog(DateTime from, DateTime to)
{
    var data = await _http.GetFromJsonAsync<List<EmployeeWorkLogModel>>(
        $"api/employeeworklog/range?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
    );

    return data ?? new List<EmployeeWorkLogModel>();
}

    public async Task<List<CompanyCalendarModel>> GetCompanyCalendar()
{
    var data = await _http.GetFromJsonAsync<List<CompanyCalendarModel>>("api/companycalendar");
    return data ?? new List<CompanyCalendarModel>();
}

public async Task<List<EmployeeAvailabilityModel>> GetEmployeeAvailability(DateTime from, DateTime to)
{
    var data = await _http.GetFromJsonAsync<List<EmployeeAvailabilityModel>>(
        $"api/employeeavailability/range?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
    );

    return data ?? new List<EmployeeAvailabilityModel>();
}
            
public async Task<Dictionary<int, DetailResult>> CalculateDetailGlobal(
    List<TaskDto> allTasksOrdered,
    List<BatchSimulationRow> orderedBatches)
{
    var result = new Dictionary<int, DetailResult>();
    var sharedEmployeeBusy = new Dictionary<int, DateTime>();
    var sharedWorkCenterBusy = new Dictionary<int, List<DateTime>>();
    var grouped = allTasksOrdered
        .GroupBy(t => t.BatchProductId)
        .ToDictionary(g => g.Key, g => g.ToList());

    foreach (var batch in orderedBatches)
    {
        var batchTasks = grouped.ContainsKey(batch.BatchProductId)
            ? grouped[batch.BatchProductId]
            : new List<TaskDto>();

        var queueIndex = orderedBatches.FindIndex(x => x.BatchProductId == batch.BatchProductId);

        // ✔ prioritāte dod reālu nobīdi (nevis 30min)
        var queueStart = DateTime.Today.AddHours(queueIndex * 4);

        var detail = await CalculateDetail(
            batchTasks,
            batch.Planned,
            queueStart,
            sharedEmployeeBusy,
            sharedWorkCenterBusy
        );

        result[batch.BatchProductId] = detail;
    }

    return result;
}
   
public class DetailResult
{
    // DETAIL
    public string? Status { get; set; }
    public double? NotStartedDays { get; set; }
    public string? FinishDateText { get; set; }
    public string? NotStartedText { get; set; }
    public DateTime? FinishDate { get; set; }

    // ASSEMBLY
    public string? AssemblyStatus { get; set; }
    public string? AssemblyTimeText { get; set; }
    public DateTime? AssemblyFinishDate { get; set; }
}
    }
}