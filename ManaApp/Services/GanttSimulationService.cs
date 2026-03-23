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
        public async Task<DetailResult> CalculateDetail(List<TaskDto> tasks, DateTime? queueStart = null)
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
                        
                    // 🔥 laiks (dienās)
                var orderedSteps = detailTasks
                    .OrderBy(t => t.StepOrder)
                    .ToList();

var started = detailTasks
    .Where(t => t.Status != 5 && t.FinishedAt != null)
    .Select(t => t.FinishedAt)
    .ToList();

var startDate = queueStart ??
    (started.Any()
        ? started.Max()!.Value
        : DateTime.Today);

    var currentTime = startDate;
    var employeeBusy = new Dictionary<int, DateTime>();
    var workCenterBusy = new Dictionary<int, List<DateTime>>();

foreach (var step in orderedSteps)
{
       // skip pabeigtos
if (step.Status == 3)
    continue;

var stepRemaining = step.EstimatedTotalMinutes - step.ActualMinutes;

if (stepRemaining < 0)
    stepRemaining = 0;

var stepDays = stepRemaining / 60.0 / 8.0;

//  ja ir darbinieks → jāgaida viņš
DateTime stepStart = currentTime;
calendarDict.TryGetValue(stepStart.Date, out var dayStart);

if (dayStart != null && dayStart.WorkStart.HasValue)
{
    var workStart = stepStart.Date.Add(dayStart.WorkStart.Value);

    if (stepStart < workStart)
    {
        stepStart = workStart;
    }
}

//  WorkCenter ierobežojums
if (step.WorkCenterId.HasValue)
{
    var wcId = step.WorkCenterId.Value;

if (!workCenterBusy.ContainsKey(wcId))
{
    var capacity = step.Capacity > 0 ? step.Capacity : 1;

    workCenterBusy[wcId] = Enumerable
        .Repeat(currentTime, capacity)
        .ToList();
}

// atrodam ātrāko brīvo slotu
var slots = workCenterBusy[wcId];
var earliest = slots.Min();

if (earliest > stepStart)
    stepStart = earliest;
}

if (step.AssignedTo.HasValue)
{
    var empId = step.AssignedTo.Value;

// availability check
if (availabilityDict.ContainsKey(empId))
{
    var safetyAvailability = 0;

    while (true)
    {
        safetyAvailability++;

        if (safetyAvailability > 365)
            break;
        var empAvailabilityList = availabilityDict[empId];

        var isAvailableNow = empAvailabilityList.Any(a =>
            a.DateFrom.Date <= stepStart.Date &&
            (a.DateTo == null || a.DateTo.Value.Date >= stepStart.Date) &&
            a.Status != "Unavailable"
        );

        if (isAvailableNow)
            break;

        stepStart = stepStart.Date.AddDays(1);
    }
}

 workLogDict.TryGetValue((empId, stepStart.Date), out var empLog);

if (empLog != null && empLog.TimeFrom.HasValue && empLog.TimeTo.HasValue)
{
    var workStart = stepStart.Date.Add(empLog.TimeFrom.Value);
    var workEnd = stepStart.Date.Add(empLog.TimeTo.Value);

    if (stepStart < workStart)
    {
        stepStart = workStart;
    }

    if (stepStart >= workEnd)
        {
            stepStart = stepStart.Date.AddDays(1);

            // pārliekam uz nākamās dienas sākumu
            workLogDict.TryGetValue((empId, stepStart.Date), out var nextDayLog);

            if (nextDayLog != null && nextDayLog.TimeFrom.HasValue)
            {
                stepStart = stepStart.Date.Add(nextDayLog.TimeFrom.Value);
            }
        }
}


        if (employeeBusy.ContainsKey(empId))
        {
            var empFreeAt = employeeBusy[empId];

            if (empFreeAt > stepStart)
                stepStart = empFreeAt;
        }

        var stepEnd = CalculateStepEnd(stepStart, stepRemaining, calendarDict);

        //  atjaunojam WorkCenter aizņemtību
            if (step.WorkCenterId.HasValue)
            {
                var slots = workCenterBusy[step.WorkCenterId.Value];

                // atrodam kuru slotu izmantot (agrāko)
                var index = slots.IndexOf(slots.Min());

                // atjaunojam to slotu
                slots[index] = stepEnd;
            }

            // atjaunojam darbinieka aizņemtību
            employeeBusy[empId] = stepEnd;

            currentTime = stepEnd;
        while (
            currentTime.DayOfWeek == DayOfWeek.Saturday ||
            currentTime.DayOfWeek == DayOfWeek.Sunday ||
            calendarDict.TryGetValue(currentTime.Date, out var dayCheck) && dayCheck.WorkStart == null
        )
        {
            currentTime = currentTime.AddDays(1);
        }

        
}
else
    {
        var stepEnd = CalculateStepEnd(stepStart, stepRemaining, calendarDict);

        currentTime = stepEnd;

        while (
            currentTime.DayOfWeek == DayOfWeek.Saturday ||
            currentTime.DayOfWeek == DayOfWeek.Sunday ||
            calendarDict.TryGetValue(currentTime.Date, out var dayCheck) && dayCheck.WorkStart == null
        )
        {
            currentTime = currentTime.AddDays(1);
        }
    }
}
if (result.Status == "Nav iesākts")
        {
            result.FinishDateText = "-";
        }
else if (result.Status == "Procesā")
            {
                var finishDate = currentTime;
                result.FinishDate = finishDate;
                result.FinishDateText = finishDate.ToString("dd.MM");
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
                                        result.FinishDateText = dt.ToString("dd.MM");
                                    }
                        }
                        else
                        {
                            result.FinishDateText = "-";
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
            
    
        public class DetailResult
        {
            public string? Status { get; set; }
            public double? NotStartedDays { get; set; }
            public string? FinishDateText { get; set; }
            public string? NotStartedText { get; set; }
            public DateTime? FinishDate { get; set; }
        }
    }
}