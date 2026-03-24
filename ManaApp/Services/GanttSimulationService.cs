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
                    var result = new DetailResult
                        {
                            Status = "-"
                        };

                    var detailTasks = tasks.Where(t => t.StepType == 1).ToList();
                    result.HasDetailNotStarted = detailTasks.Any(t => t.Status == 5);

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

//  Detail laiku ŠEIT vairs neuzstādām (to darīs CalculateDetailGlobal)

if (result.Status == "Pabeigts" && result.FinishDate == null)
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
}


// =======================
// ASSEMBLY STATUS LOĢIKA
// =======================

CalculateFinishing(result, tasks, calendarDict, startDate);

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

private Dictionary<int, SimulationResult> SimulateAllSteps(
    List<TaskDto> allStepsQueue,
    Dictionary<int, DateTime> sharedEmployeeBusy,
    Dictionary<int, List<DateTime>> sharedWorkCenterBusy,
    Dictionary<DateTime, CompanyCalendarModel> calendarDict,
    Dictionary<int, DateTime> batchStartMap)
{
    var result = new Dictionary<int, SimulationResult>();
    var partStepEndTimes = new Dictionary<(int batchId, int partId), DateTime>();

    var availableSteps = new List<TaskDto>();

    // 🔥 sākam ar pirmajiem step (katram part)
availableSteps.AddRange(
    allStepsQueue
        .GroupBy(s => new { s.BatchProductId, s.ProductToPartId })
        .Select(g => g
            .Where(s => s.Status != 3)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault())
        .Where(x => x != null)
        .Select(x => x!)
);

while (availableSteps.Any())
{

    var step = availableSteps
        .Select(s =>
        {

            DateTime possibleStart = DateTime.Today;

            // 🔴 ja jau ir paveikts → sākam no fakta
            if (s.FinishedAt.HasValue)
            {
                possibleStart = s.FinishedAt.Value;
            }

            if (batchStartMap.ContainsKey(s.BatchProductId))
                {
                    var batchStart = batchStartMap[s.BatchProductId];
                    if (batchStart > possibleStart)
                        possibleStart = batchStart;
                }

                if (partStepEndTimes.ContainsKey((s.BatchProductId, s.ProductToPartId)))
                {
                    var chainEnd = partStepEndTimes[(s.BatchProductId, s.ProductToPartId)];

                    if (chainEnd > possibleStart)
                        possibleStart = chainEnd;
                }

            if (s.AssignedTo.HasValue && sharedEmployeeBusy.ContainsKey(s.AssignedTo.Value))
                {
                    var empFree = sharedEmployeeBusy[s.AssignedTo.Value];

                    if (empFree > possibleStart)
                        possibleStart = empFree;
                }

            if (s.WorkCenterId.HasValue && sharedWorkCenterBusy.ContainsKey(s.WorkCenterId.Value))
            {
                var wcFree = sharedWorkCenterBusy[s.WorkCenterId.Value].Min();
                if (wcFree > possibleStart)
                    possibleStart = wcFree;
            }

            return new
            {
                Step = s,
                Start = possibleStart
            };
        })
            .OrderBy(x => x.Start)
            .ThenByDescending(x => x.Step.TasksPush)
            .ThenByDescending(x => x.Step.PriorityLevel)
            .ThenByDescending(x => x.Step.TasksPriority)
            .ThenBy(x => x.Step.IsPriority ? 0 : 1)
            .ThenBy(x => x.Step.Priority)
            .Select(x => x.Step)
            .First();;

    availableSteps.Remove(step);

DateTime stepStart = DateTime.MinValue;

//  1) ja step jau bijis → ņem faktisko
if (step.FinishedAt.HasValue)
{
    stepStart = step.FinishedAt.Value;
}

//  2) chain (iepriekšējais step)
if (partStepEndTimes.ContainsKey((step.BatchProductId, step.ProductToPartId)))
{
    var chainEnd = partStepEndTimes[(step.BatchProductId, step.ProductToPartId)];
    if (chainEnd > stepStart)
        stepStart = chainEnd;
}

//  3) batch start
if (batchStartMap.ContainsKey(step.BatchProductId))
{
    var batchStart = batchStartMap[step.BatchProductId];
    if (batchStart > stepStart)
        stepStart = batchStart;
}

//  4) fallback → Today
if (stepStart == DateTime.MinValue)
{
    stepStart = DateTime.Today;
}

    if (step.AssignedTo.HasValue)
        {
            var empId = step.AssignedTo.Value;

            if (sharedEmployeeBusy.ContainsKey(empId))
            {
                var empFreeAt = sharedEmployeeBusy[empId];
                if (empFreeAt > stepStart)
                    stepStart = empFreeAt;
            }
        }

    if (step.WorkCenterId.HasValue)
        {
            var wcId = step.WorkCenterId.Value;

            if (!sharedWorkCenterBusy.ContainsKey(wcId))
            {
                var capacity = step.Capacity > 0 ? step.Capacity : 1;

                sharedWorkCenterBusy[wcId] = Enumerable
                    .Repeat(stepStart, capacity)
                    .ToList();
            }

            var slots = sharedWorkCenterBusy[wcId];
            var earliest = slots.Min();

            if (earliest > stepStart)
                stepStart = earliest;
        }
// 🔥 Assembly nedrīkst sākties pirms Detail pabeigts (FINAL CHECK)
if (step.StepType == 2)
{
    if (result.ContainsKey(step.BatchProductId))
    {
        var detailFinish = result[step.BatchProductId].DetailFinish;

        if (detailFinish.HasValue && detailFinish.Value > stepStart)
        {
            stepStart = detailFinish.Value;
        }
    }
}

DateTime stepEnd;

// 🔴 JA PABEIGTS → ņem DB laiku, NESIMULĒ
if (step.Status == 3 && step.FinishedAt.HasValue)
{
    stepEnd = step.FinishedAt.Value;

    // 🔥 NEKAD neļaujam būt pirms stepStart
    if (stepEnd < stepStart)
    {
        stepEnd = stepStart;
    }
}

else
{
    var stepRemaining = step.EstimatedTotalMinutes - step.ActualMinutes;
    if (stepRemaining < 0) stepRemaining = 0;

    stepEnd = CalculateStepEnd(stepStart, stepRemaining, calendarDict);
}

    if (step.WorkCenterId.HasValue)
        {
            var slots = sharedWorkCenterBusy[step.WorkCenterId.Value];
            var index = slots.IndexOf(slots.Min());
            slots[index] = stepEnd;
        }

if (step.AssignedTo.HasValue)
{
    var empId = step.AssignedTo.Value;

    sharedEmployeeBusy[empId] = stepEnd;
}

    partStepEndTimes[(step.BatchProductId, step.ProductToPartId)] = stepEnd;

if (step.IsFinal)
{
    if (!result.ContainsKey(step.BatchProductId))
        result[step.BatchProductId] = new SimulationResult();

    if (step.StepType == 1)
    {
        var current = result[step.BatchProductId].DetailFinish;
        if (!current.HasValue || stepEnd > current.Value)
            result[step.BatchProductId].DetailFinish = stepEnd;
    }

    if (step.StepType == 2)
    {
        var current = result[step.BatchProductId].AssemblyFinish;
        if (!current.HasValue || stepEnd > current.Value)
            result[step.BatchProductId].AssemblyFinish = stepEnd;
    }
}

var nextStep = allStepsQueue
    .Where(s =>
        s.BatchProductId == step.BatchProductId &&
        s.ProductToPartId == step.ProductToPartId &&
        s.StepOrder > step.StepOrder &&
        s.Status != 3)
    .OrderBy(s => s.StepOrder)
    .FirstOrDefault();

    if (nextStep != null)
    {
        availableSteps.Add(nextStep);
    }
}

    return result;
}

private void CalculateAssembly(
    DetailResult result,
    List<TaskDto> tasks,
    List<TaskDto> detailTasks,
    Dictionary<DateTime, CompanyCalendarModel> calendarDict,
    DateTime startDate)
{
    var assemblyTasks = tasks.Where(t => t.StepType == 2).ToList();

    if (!assemblyTasks.Any())
    {
        result.AssemblyStatus = "-";
        result.AssemblyTimeText = "-";
        return;
    }

    var detailStatuses = detailTasks.Select(t => t.Status).ToList();
    var assemblyStatuses = assemblyTasks.Select(t => t.Status).ToList();

    var hasDetailNotStarted = detailStatuses.Any(s => s == 5);
    var detailAllFinished = detailStatuses.All(s => s == 3);

    var hasAssemblyActive = assemblyStatuses.Any(s => s == 1 || s == 2);
    
    var assemblyAllFinished = assemblyStatuses.All(s => s == 3);

    // 🔵 4) Assembly pabeigts → DB
    if (assemblyAllFinished)
    {
        result.AssemblyStatus = "Pabeigts";

        var last = assemblyTasks
            .Where(t => t.FinishedAt != null)
            .Select(t => t.FinishedAt!.Value)
            .Max();

        result.AssemblyFinishDate = last;
        result.AssemblyTimeText = last.ToString("dd.MM.yyyy");
        return;
    }


// 🔴 Detail nav pilnībā pabeigts → Assembly GAIDA
if (!detailAllFinished)
{
    result.AssemblyStatus = "Gaida";

    // ja Detail vēl ir neiesākti step, rādam tikai Assembly ilgumu
    if (detailStatuses.Any(s => s == 5))
    {
        var totalMinutes = assemblyTasks
            .Sum(t => Math.Max(0, t.EstimatedTotalMinutes - t.ActualMinutes));

        if (totalMinutes == 0)
        {
            result.AssemblyTimeText = "-";
            return;
        }

        var d = totalMinutes / (8 * 60);
        var h = (totalMinutes % (8 * 60)) / 60;
        var m = totalMinutes % 60;

        result.AssemblyTimeText = $"{d}d {h}h {m}m";
        return;
    }

// ja Detail vairs nav neviena 5, rādam simulēto Assembly datumu
if (result.AssemblyFinishDate.HasValue)
{
    var assemblyFinish = result.AssemblyFinishDate.Value;

    // 🔥 obligāti jābūt pēc Detail
    if (result.FinishDate.HasValue)
    {
        var minStart = result.FinishDate.Value;

        // NEĻAUJAM sakrist vai būt pirms
        if (assemblyFinish <= minStart)
        {
            assemblyFinish = CalculateStepEnd(
                minStart,
                assemblyTasks.Sum(t => Math.Max(0, t.EstimatedTotalMinutes - t.ActualMinutes)),
                calendarDict
            );
        }
    }

    result.AssemblyTimeText = assemblyFinish.ToString("dd.MM.yyyy");
    return;
}

result.AssemblyTimeText = "-";
return;
}

    // 🟢 3) Detail visi 3 → sāk no DB finish
DateTime assemblyStart;

// 🔥 Assembly NEKAD nedrīkst sākties pirms Detail finish

var baseStart = startDate;

// ja ir detail finish → tas ir obligāts minimums
if (result.FinishDate.HasValue)
{
    baseStart = result.FinishDate.Value > baseStart
        ? result.FinishDate.Value
        : baseStart;
}

assemblyStart = baseStart;

DateTime finish;

if (result.AssemblyFinishDate.HasValue)
{
    finish = result.AssemblyFinishDate.Value;

    // 🔥 NEKAD nedrīkst būt ātrāk par Detail
    if (result.FinishDate.HasValue && finish < result.FinishDate.Value)
    {
        finish = result.FinishDate.Value;
    }
}

else
{
    var remaining = assemblyTasks
        .Sum(t => Math.Max(0, t.EstimatedTotalMinutes - t.ActualMinutes));

    finish = CalculateStepEnd(assemblyStart, remaining, calendarDict);
}

if (result.FinishDate.HasValue)
{
    finish = finish < result.FinishDate.Value
        ? result.FinishDate.Value
        : finish;
}

result.AssemblyFinishDate = finish;
result.AssemblyTimeText = finish.ToString("dd.MM.yyyy");

// 🟡 Assembly procesā
        if (hasAssemblyActive)
        {
            result.AssemblyStatus = "Procesā";
        }

// fallback statuss
if (string.IsNullOrEmpty(result.AssemblyStatus))
{
    result.AssemblyStatus = "Procesā";
}

var startedAssembly = assemblyTasks
    .Where(t => t.Status != 5 && t.FinishedAt != null)
    .Select(t => t.FinishedAt!.Value);

if (startedAssembly.Any())
{
    assemblyStart = startedAssembly.Max() > assemblyStart
        ? startedAssembly.Max()
        : assemblyStart;
}

}


private void CalculateFinishing(
    DetailResult result,
    List<TaskDto> tasks,
    Dictionary<DateTime, CompanyCalendarModel> calendarDict,
    DateTime startDate)
{
    var finishingTasks = tasks.Where(t => t.StepType == 3).ToList();

    if (!finishingTasks.Any())
    {
        return;
    }

    // TODO: te nāks FINISHING loģika
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
        var allStepsQueue = allTasksOrdered
        .OrderByDescending(t => t.IsPriority)
        .ThenBy(t => t.Priority)
        .ThenByDescending(t => t.TasksPush)
        .ThenByDescending(t => t.PriorityLevel)
        .ThenByDescending(t => t.TasksPriority)
        .ThenBy(t => t.BatchProductId)
        .ThenBy(t => t.StepOrder)
        .ToList();
    var calendar = _calendarCache ??= await GetCompanyCalendar();
    var calendarDict = calendar.ToDictionary(c => c.WorkDate.Date);

var batchStartMap = orderedBatches
    .ToDictionary(
        x => x.BatchProductId,
        x =>
        {
            var batchTasks = allTasksOrdered
                .Where(t => t.BatchProductId == x.BatchProductId && t.StepType == 1);

            var lastFinished = batchTasks
                .Where(t => t.FinishedAt != null)
                .Select(t => t.FinishedAt!.Value)
                .DefaultIfEmpty(DateTime.Today)
                .Max();

            return lastFinished;
        }
    );

    var simulatedBatchFinish = SimulateAllSteps(
        allStepsQueue,
        sharedEmployeeBusy,
        sharedWorkCenterBusy,
        calendarDict,
        batchStartMap
    );
    var grouped = allTasksOrdered
        .GroupBy(t => t.BatchProductId)
        .ToDictionary(g => g.Key, g => g.ToList());
    // 🔥 VISI STEP vienā rindā (Detail + Assembly + Finishing)


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
    queueStart
);

// =========================
// 🔵 DETAIL FINISH KOREKCIJA
// =========================

if (simulatedBatchFinish.ContainsKey(batch.BatchProductId))
{
    var sim = simulatedBatchFinish[batch.BatchProductId];

    if (detail.Status == "Pabeigts")
    {
        // neko
    }
    else if (detail.HasDetailNotStarted)
    {
        var detailSteps = batchTasks.Where(t => t.StepType == 1).ToList();

        var hasActive = detailSteps.Any(t => t.Status == 1 || t.Status == 2);
        var hasFinished = detailSteps.Any(t => t.Status == 3);
        var hasOnlyFinishedAndNotStarted =
            !hasActive && hasFinished && detailSteps.Any(t => t.Status == 5);

        if (hasActive && sim.DetailFinish.HasValue)
        {
            detail.FinishDate = sim.DetailFinish.Value;
            detail.FinishDateText = sim.DetailFinish.Value.ToString("dd.MM.yyyy");
        }
        else if (hasOnlyFinishedAndNotStarted)
        {
            var lastFinished = detailSteps
                .Where(t => t.Status == 3 && t.FinishedAt != null)
                .Select(t => t.FinishedAt!.Value)
                .Max();

            detail.FinishDate = lastFinished;
            detail.FinishDateText = lastFinished.ToString("dd.MM.yyyy");
        }
    }
    else
    {
        if (sim.DetailFinish.HasValue)
        {
            detail.FinishDate = sim.DetailFinish.Value;
            detail.FinishDateText = sim.DetailFinish.Value.ToString("dd.MM.yyyy");
        }
    }
}

// =========================
// 🔥 TAGAD tikai rēķinam Assembly
// =========================
if (simulatedBatchFinish.ContainsKey(batch.BatchProductId))
{
    var sim = simulatedBatchFinish[batch.BatchProductId];

    if (sim.AssemblyFinish.HasValue)
    {
        detail.AssemblyFinishDate = sim.AssemblyFinish.Value;
    }
}

CalculateAssembly(
    detail,
    batchTasks,
    batchTasks.Where(t => t.StepType == 1).ToList(),
    calendarDict,
    queueStart
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
    public bool HasDetailNotStarted { get; set; }

    // FINISHING
    public string? FinishingStatus { get; set; }
    public string? FinishingTimeText { get; set; }
    public DateTime? FinishingFinishDate { get; set; }
}

public class SimulationResult
{
    public DateTime? DetailFinish { get; set; }
    public DateTime? AssemblyFinish { get; set; }
}

    }
}