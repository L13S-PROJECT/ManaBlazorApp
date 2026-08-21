// TopPartsController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;
using ManaApp.Shared.DTOs.Planning;
using ManiApi.DTOs.Workflow;
using ManaApp.Shared.DTOs.TopPart;
using ManiApi.Services.TopParts;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopPartsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TopPartWorkflowService _topPartWorkflowService;

        public TopPartsController(
            AppDbContext db,
            TopPartWorkflowService topPartWorkflowService)
        {
            _db = db;
            _topPartWorkflowService = topPartWorkflowService;
        }

        // GET: api/topparts
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] TopPartType? type = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] string? search = null,
            [FromQuery] uint? relatedTopPartId = null)
        {
            var query = _db.TopParts
                .Where(x => x.IsActive);

                if (type.HasValue)
                    {
                        query = query.Where(x => x.TopPartType == type.Value);
                    }

                if (categoryId.HasValue)
                    {
                        query = query.Where(x => x.TopPartCategoryID == categoryId.Value);
                    }

                if (!string.IsNullOrWhiteSpace(search))
                    {
                        search = search.Trim();

                        query = query.Where(x =>
                            x.TopPartName.Contains(search) ||
                            x.TopPartCode.Contains(search));
                    }

            var releasedWorkflows = _db.Workflows
                .Where(w =>
                    w.Status == WorkflowStatus.Released &&
                    w.IsCurrent &&
                    w.IsActive);

            var releasedWorkflowIds = await releasedWorkflows
                .Select(w => w.Id)
                .ToListAsync();

            var topPartRelations = await _db.WorkflowComponents
                .Where(c =>
                    c.IsActive &&
                    c.TopPartId.HasValue &&
                    releasedWorkflowIds.Contains(c.WorkflowId))
                .Select(c => new
                {
                    c.WorkflowId,
                    TopPartId = c.TopPartId!.Value
                })
                .Distinct()
                .ToListAsync();

            var workflowTopPartMap = await releasedWorkflows
                .Where(w => w.TopPartId.HasValue)
                .Select(w => new
                {
                    WorkflowId = w.Id,
                    TopPartId = w.TopPartId!.Value
                })
                .ToDictionaryAsync(
                    x => x.WorkflowId,
                    x => x.TopPartId);

            var topPartLinks = topPartRelations
                .Where(x => workflowTopPartMap.ContainsKey(x.WorkflowId))
                .Select(x => new
                {
                    FromTopPartId = workflowTopPartMap[x.WorkflowId],
                    ToTopPartId = x.TopPartId
                })
                .Where(x => x.FromTopPartId != x.ToTopPartId)
                .Distinct()
                .ToList();
            
            var relatedTopPartIds = new HashSet<uint>();

                if (relatedTopPartId.HasValue)
                {
                    var pending = new Stack<uint>();
                    pending.Push(relatedTopPartId.Value);

                    while (pending.Count > 0)
                    {
                        var currentId = pending.Pop();

                        if (!relatedTopPartIds.Add(currentId))
                            continue;

                        foreach (var link in topPartLinks)
                        {
                            if (link.FromTopPartId == currentId)
                                pending.Push(link.ToTopPartId);

                            if (link.ToTopPartId == currentId)
                                pending.Push(link.FromTopPartId);
                        }
                    }
                }

            var rows = await query
                .Select(x => new TopPartListItemDto
                {
                    Id = x.Id,
                    TopPartName = x.TopPartName,
                    TopPartCode = x.TopPartCode,
                    TopPartType = (byte)x.TopPartType,
                    TopPartCategoryID = x.TopPartCategoryID,
                    CategoryID = x.CategoryID,
                    Description = x.Description,
                    DraftCreatedDate = _db.Workflows
                        .Where(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Draft &&
                            w.IsActive)
                        .Select(w => w.CreatedDate)
                        .FirstOrDefault(),

                    ReleasedVersion = _db.Workflows
                        .Where(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Released &&
                            w.IsCurrent &&
                            w.IsActive)
                        .OrderByDescending(w => w.WorkflowVersion)
                        .Select(w => (int?)w.WorkflowVersion)
                        .FirstOrDefault(),

                    ReleasedDate = _db.Workflows
                        .Where(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Released &&
                            w.IsCurrent &&
                            w.IsActive)
                        .OrderByDescending(w => w.WorkflowVersion)
                        .Select(w => w.ReleasedDate)
                        .FirstOrDefault(),
                    
                    FlowComment = _db.Workflows
                        .Where(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Released &&
                            w.IsActive)
                        .OrderByDescending(w => w.WorkflowVersion)
                        .Select(w => w.Description)
                        .FirstOrDefault(),
                    
                    FlowStatus =
                        _db.Workflows.Any(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Released &&
                            w.IsCurrent &&
                            w.IsActive)
                            ? (_db.Workflows.Any(w =>
                                w.TopPartId == x.Id &&
                                w.Status == WorkflowStatus.Draft &&
                                w.IsActive)
                                    ? (byte)1
                                    : (byte)2)
                            : (byte)0})
                .ToListAsync();

            if (relatedTopPartId.HasValue)
                {
                    rows = rows
                        .Where(x => relatedTopPartIds.Contains((uint)x.Id))
                        .ToList();
                }
            
            var rowById = rows.ToDictionary(
                x => (uint)x.Id,
                x => x);

            var linkedTopPartIds = topPartLinks
                .Where(x => rowById.ContainsKey(x.FromTopPartId))
                .Select(x => x.ToTopPartId)
                .ToHashSet();

            var rootTopParts = rows
                .Where(x => !linkedTopPartIds.Contains((uint)x.Id))
                .OrderBy(x => x.TopPartType switch
                    {
                        1 => 0, // Product
                        0 => 1, // Part
                        2 => 2, // SparePart
                        _ => 3
                    })
                .ThenBy(x => x.TopPartName)
                .ToList();

            var orderedRows = new List<TopPartListItemDto>();
            var visitedTopPartIds = new HashSet<uint>();

            void AddTopPartRecursive(uint topPartId)
                {
                    if (!visitedTopPartIds.Add(topPartId))
                        return;

                    if (!rowById.TryGetValue(topPartId, out var topPart))
                        return;

                    orderedRows.Add(topPart);

                    var linkedIds = topPartLinks
                        .Where(x => x.FromTopPartId == topPartId)
                        .Select(x => x.ToTopPartId)
                        .Distinct();

                    foreach (var linkedId in linkedIds)
                    {
                        AddTopPartRecursive(linkedId);
                    }
                }

            foreach (var root in rootTopParts)
                {
                    AddTopPartRecursive((uint)root.Id);
                }

            return Ok(orderedRows);
        }

        // POST: api/topparts
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateTopPartDto dto)
{
    await using var transaction =
        await _db.Database.BeginTransactionAsync();

    if (string.IsNullOrWhiteSpace(dto.TopPartName))
        return BadRequest("Nosaukums ir obligāts.");

    if (string.IsNullOrWhiteSpace(dto.TopPartCode))
        return BadRequest("Kods ir obligāts.");
    
    dto.TopPartCode = dto.TopPartCode.Trim().ToUpper();

    if (dto.TopPartCategoryID is null)
        return BadRequest("Kategorija ir obligāta.");

    if (dto.CategoryID.HasValue)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(x => x.Id == dto.CategoryID.Value && x.IsActive);

            if (!categoryExists)
                return BadRequest("Izvēlētā preču grupa neeksistē vai nav aktīva.");
        }
    
    if (dto.TopPartType is null)
        return BadRequest("Tips ir obligāts.");

    if (dto.TopPartType == (byte)TopPartType.Part)
        {
            dto.CategoryID = null;
        }
    else if (dto.CategoryID is null)
        {
            return BadRequest("Kategorija ir obligāta.");
        }

    var exists = await _db.TopParts
        .AnyAsync(x => x.TopPartCode == dto.TopPartCode);

    if (exists)
        return Conflict("Šāds kods jau eksistē.");

    dto.TopPartName = dto.TopPartName.Trim();

    var topPart = new TopPart
        {
            TopPartName = dto.TopPartName,
            TopPartCode = dto.TopPartCode,
            TopPartType = (TopPartType)dto.TopPartType!.Value,
            TopPartCategoryID = dto.TopPartCategoryID,
            CategoryID = dto.CategoryID,
            Description = dto.Description,
            Stage = 1,
            IsActive = true
        };

    _db.TopParts.Add(topPart);
    await _db.SaveChangesAsync();

    await _topPartWorkflowService.CreateInitialWorkflowAsync(
        topPart.Id,
        topPart.TopPartName);

    await transaction.CommitAsync();

    return Ok(topPart);

}

        // PUT: api/topparts
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateTopPartDto dto)
        {
            var row = await _db.TopParts
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.IsActive);

            if (row is null)
                return NotFound();
            
            if (string.IsNullOrWhiteSpace(dto.TopPartCode))
                return BadRequest("Kods ir obligāts.");

            if (dto.TopPartCategoryID is null)
                return BadRequest("Kategorija ir obligāta.");

            dto.TopPartName = dto.TopPartName.Trim();
            dto.TopPartCode = dto.TopPartCode.Trim().ToUpper();

            var codeExists = await _db.TopParts
                .AnyAsync(x => x.Id != dto.Id &&
                            x.TopPartCode == dto.TopPartCode);

            if (codeExists)
                return Conflict("Šāds kods jau eksistē.");

            if (string.IsNullOrWhiteSpace(dto.TopPartName))
                return BadRequest("Nosaukums ir obligāts.");

            row.TopPartName = dto.TopPartName;
            row.TopPartCode = dto.TopPartCode;
            row.TopPartCategoryID = dto.TopPartCategoryID;
            row.Description = dto.Description?.Trim();

            await _db.SaveChangesAsync();

            return Ok(new TopPartListItemDto
                {
                    Id = row.Id,
                    TopPartName = row.TopPartName,
                    TopPartCode = row.TopPartCode,
                    TopPartType = (byte)row.TopPartType,
                    TopPartCategoryID = row.TopPartCategoryID,
                    CategoryID = row.CategoryID,
                    Description = row.Description
                });

        }

        // DELETE: api/topparts/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _db.TopParts
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

            if (row is null)
                return NotFound();

            row.IsActive = false;
            await _db.SaveChangesAsync();

            return Ok();
        }

        // GET: api/topparts/by-version?versionId=5
[HttpGet("by-version")]
public async Task<IActionResult> GetByVersion(int versionId)
{
    var rows = await (
        from ptp in _db.ProductTopParts
        join tp in _db.TopParts on ptp.TopPartId equals tp.Id
        where ptp.VersionId == versionId 
            && ptp.IsActive
            && tp.Stage == 1
        select new ProductToPartDto
{
    Id = ptp.Id,
    TopPart_Id = tp.Id,
    TopPart_Name = tp.TopPartName,

    OrderQty =
            (
                from oi in _db.OrderItems

                join map in _db.CustomerCodeMaps
                    on oi.CustomerCodeMapId equals map.Id

                where
                    oi.IsActive &&
                    map.ProductToPartId == ptp.Id &&
                    map.VersionId == ptp.VersionId &&
                    map.RalColorId != null

                select (int?)oi.Quantity
            ).Sum() ?? 0,

ProductOrderQty =

(
    from oi in _db.OrderItems

    join map in _db.CustomerCodeMaps
        on oi.CustomerCodeMapId equals map.Id

    where
        oi.IsActive &&
        map.VersionId == ptp.VersionId &&
        map.ProductToPartId == null

    select (int?)oi.Quantity
).Sum() ?? 0,

})
    .OrderBy(x => x.TopPart_Name)
    .ToListAsync();

    var productRalRows = await (
    from oi in _db.OrderItems

    join map in _db.CustomerCodeMaps
        on oi.CustomerCodeMapId equals map.Id

    join ral in _db.RalColors
        on map.RalColorId equals ral.ID

    where
        oi.IsActive &&
        map.VersionId == versionId &&
        map.ProductToPartId == null &&
        map.RalColorId != null

    group oi by new
            {
                ral.Name
            }
            into g

            select new RalRowDto
            {
                RalCode = g.Key.Name,
                RalName = g.Key.Name,
                Qty = g.Sum(x => x.Quantity)
            }

).ToListAsync();

    foreach (var row in rows)
{
    
    row.PartRalRows = await (
        from oi in _db.OrderItems

        join map in _db.CustomerCodeMaps
            on oi.CustomerCodeMapId equals map.Id

        join ral in _db.RalColors
            on map.RalColorId equals ral.ID

        where
            oi.IsActive &&
            map.ProductToPartId == row.Id &&
            map.RalColorId != null

        group oi by ral.Name into g

        select new RalRowDto
        {
            RalCode = g.Key,
            Qty = g.Sum(x => x.Quantity)
        }
    ).ToListAsync();

    row.ProductRalRows = productRalRows;
}

    return Ok(rows);
}

[HttpGet("stock-from-movements")]
public async Task<IActionResult> GetStockFromMovements([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var rows = await (
        from ptp in _db.ProductTopParts
        join tp in _db.TopParts on ptp.TopPartId equals tp.Id
        where ptp.VersionId == versionId
              && ptp.IsActive
              && tp.IsActive
              && tp.Stage == 1
        select new
        {
            TopPartId = tp.Id,
            TopPartName = tp.TopPartName,

            StockQty =
            _db.StockMovements
                .Where(sm =>
                    sm.IsActive &&
                    sm.Version_ID == ptp.VersionId &&
                    sm.BatchProduct_ID == ptp.Id)
                .Sum(sm => (int?)sm.Stock_Qty) ?? 0
        }
    ).ToListAsync();

    return Ok(rows);
}

[HttpGet("planned-parts")]
public async Task<IActionResult> GetPlannedParts([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

            var data = await (
                from bp in _db.BatchProducts

                join ptp in _db.ProductTopParts
                    on bp.ProductToPart_ID equals ptp.Id into ptpGroup
                from ptp in ptpGroup.DefaultIfEmpty()

                join tp in _db.TopParts
                    on ptp.TopPartId equals tp.Id into tpGroup
                from tp in tpGroup.DefaultIfEmpty()

                where bp.Version_Id == versionId
                    && bp.IsActive
                    && ptp != null   // ✅ TIKAI detaļas

                group bp by new 
                { 
                    bp.Version_Id, 
                    TopPartId = tp != null ? tp.Id : bp.ProductToPart_ID 
                } into g

                select new
                {
                    VersionId = g.Key.Version_Id,
                    TopPartId = g.Key.TopPartId,
                    PlannedQty = g.Sum(x => x.Planned_Qty)
                }
            ).ToListAsync();

    return Ok(data);
}

[HttpGet("planned-correct")]
public async Task<IActionResult> GetPlannedCorrect([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

var data = await (
    from bp in _db.BatchProducts

    join ptp in _db.ProductTopParts
        on bp.ProductToPart_ID equals ptp.Id

    where bp.IsActive
      && bp.Version_Id == versionId
      && ptp.IsActive
      && bp.ProductToPart_ID != null

        && !_db.StockMovements
            .Any(sm =>
                sm.IsActive &&
                sm.BatchProduct_ID == bp.ID &&
                sm.Move_Type != MoveType.PLANNED)

          // tikai DETAIL tasks = 5
          && !_db.Tasks
                .Join(_db.TopPartSteps,
                    t => t.TopPartStep_ID,
                    ts => ts.Id,
                    (t, ts) => new { t, ts })
                .Any(x =>
                    x.t.BatchProduct_ID == bp.ID &&
                    x.t.IsActive &&
                    x.ts.StepType == 1 &&
                    x.t.Tasks_Status != 5)

    group bp by new
    {
        bp.Version_Id,
        ptp.TopPartId
    } into g

    select new
    {
        VersionId = g.Key.Version_Id,
        TopPartId = g.Key.TopPartId,
        PlannedQty = g.Sum(x => x.Planned_Qty)
    }
).ToListAsync();

    return Ok(data);
}

[HttpGet("in-production-correct")]
public async Task<IActionResult> GetInProductionCorrect([FromQuery] int versionId)
{
    var query = _db.BatchProducts.AsQueryable();

if (versionId > 0)
    query = query.Where(x => x.Version_Id == versionId);

    var data = await (
    from bp in query

    join ptp in _db.ProductTopParts
        on bp.ProductToPart_ID equals ptp.Id

    where bp.IsActive
          && ptp.IsActive
          && bp.ProductToPart_ID != null

          && _db.Tasks
            .Join(_db.TopPartSteps,
                t => t.TopPartStep_ID,
                ts => ts.Id,
                (t, ts) => new { t, ts })
            .Where(x =>
                x.t.BatchProduct_ID == bp.ID &&
                x.t.IsActive &&
                x.ts.StepType == 1 &&
                x.ts.IsFinal)
            .Any(x => x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3)

        && _db.Tasks
            .Join(_db.TopPartSteps,
                t => t.TopPartStep_ID,
                ts => ts.Id,
                (t, ts) => new { t, ts })
            .Where(x =>
                x.t.BatchProduct_ID == bp.ID &&
                x.t.IsActive &&
                x.ts.StepType == 1 &&
                x.ts.IsFinal)
            .Any(x => x.t.Tasks_Status != 3)

    group bp by new
    {
        bp.Version_Id,
        ptp.TopPartId
    } into g

    select new
    {
        VersionId = g.Key.Version_Id,
        TopPartId = g.Key.TopPartId,
        InProduction = g.Sum(x => x.Planned_Qty)
    }
).ToListAsync();

    return Ok(data);
}

[HttpGet("ok-correct")]
public async Task<IActionResult> GetOkCorrect([FromQuery] int versionId)
{
    var query = _db.BatchProducts.AsQueryable();

    if (versionId > 0)
        query = query.Where(x => x.Version_Id == versionId);

    var data = await (
        from bp in query

        join ptp in _db.ProductTopParts
            on bp.ProductToPart_ID equals ptp.Id

        where bp.IsActive
              && ptp.IsActive
              && bp.ProductToPart_ID != null

              // VISI DETAIL taski = 3
              && !_db.Tasks
                    .Join(_db.TopPartSteps,
                        t => t.TopPartStep_ID,
                        ts => ts.Id,
                        (t, ts) => new { t, ts })
                    .Any(x =>
                        x.t.BatchProduct_ID == bp.ID &&
                        x.t.IsActive &&
                        x.ts.StepType == 1 &&
                        x.t.Tasks_Status != 3)

        group bp by new
        {
            bp.Version_Id,
            ptp.TopPartId
        } into g

        select new
        {
            VersionId = g.Key.Version_Id,
            TopPartId = g.Key.TopPartId,
            OkQty = g.Sum(x => x.Planned_Qty)
        }
    ).ToListAsync();

    return Ok(data);
}

[HttpGet("free-correct")]
public async Task<IActionResult> GetFreeCorrect([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var usedMap = await _db.Tasks
        .Join(_db.TopPartSteps,
            t => t.TopPartStep_ID,
            ts => ts.Id,
            (t, ts) => new { t, ts })
        .Where(x =>
                x.t.IsActive &&
                x.ts.StepType == 2 &&
                (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3) &&
                x.t.Qty_Done > 0
            )

        .GroupBy(x => new
        {
            x.t.BatchProduct_ID,
            x.ts.ProductToPartId
        })
        .Select(g => new
        {
            g.Key.BatchProduct_ID,
            g.Key.ProductToPartId,
            Used = g.Sum(x => (int?)x.t.Qty_Done) ?? 0
        })
        .ToListAsync();

    var usedDict = usedMap.ToDictionary(
        x => $"{x.BatchProduct_ID}_{x.ProductToPartId}",
        x => x.Used);

    var rawData = await (
    from bp in _db.BatchProducts
    where bp.IsActive
          && bp.Version_Id == versionId
          && bp.ProductToPart_ID == null

    from ptp in _db.ProductTopParts
    where ptp.VersionId == bp.Version_Id
          && ptp.IsActive

          && _db.Tasks
              .Join(_db.TopPartSteps,
                  t => t.TopPartStep_ID,
                  ts => ts.Id,
                  (t, ts) => new { t, ts })
              .Any(x =>
                  x.t.BatchProduct_ID == bp.ID &&
                  x.t.IsActive &&
                  x.ts.StepType == 1 &&
                  x.ts.ProductToPartId == ptp.Id)

          && !_db.Tasks
              .Join(_db.TopPartSteps,
                  t => t.TopPartStep_ID,
                  ts => ts.Id,
                  (t, ts) => new { t, ts })
              .Any(x =>
                  x.t.BatchProduct_ID == bp.ID &&
                  x.t.IsActive &&
                  x.ts.StepType == 2 &&
                  x.ts.ProductToPartId == ptp.Id &&
                  (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3))

    select new
    {
        bpId = bp.ID,
        VersionId = bp.Version_Id,
        ptp.TopPartId,
        ProductToPartId = ptp.Id,
        Qty = bp.Planned_Qty * ptp.QtyPerProduct
    }
).ToListAsync();

var data = rawData
    .GroupBy(x => new { x.VersionId, x.TopPartId })
    .Select(g => new
    {
        VersionId = g.Key.VersionId,
        TopPartId = g.Key.TopPartId,
        FreeQty = g.Sum(x =>
            x.Qty - (
                usedDict.TryGetValue($"{x.bpId}_{x.ProductToPartId}", out var used)
                    ? used
                    : 0))
    })
    .ToList();

    return Ok(data);
}

[HttpGet("full-data")]
public async Task<IActionResult> GetFullData([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var tasksQuery =
    _db.Tasks
        .Join(_db.TopPartSteps,
            t => t.TopPartStep_ID,
            ts => ts.Id,
            (t, ts) => new { t, ts })
        .Where(x => x.t.IsActive);

    var parts = await (
    from ptp in _db.ProductTopParts.AsNoTracking()
        join tp in _db.TopParts on ptp.TopPartId equals tp.Id
        where ptp.VersionId == versionId
              && ptp.IsActive
              && tp.IsActive
        select new
            {
                ptp.Id,
                ptp.TopPartId,
                tp.TopPartName,

                StockQty =
                    _db.StockMovements
                        .Where(sm =>
                            sm.IsActive &&
                            sm.Version_ID == ptp.VersionId &&
                            sm.BatchProduct_ID == ptp.Id)
                        .Sum(sm => (int?)sm.Stock_Qty) ?? 0,
                PlannedQty =
                    (
                        from bp in _db.BatchProducts
                        join b in _db.Batches
                            on bp.Batch_Id equals b.ID
                        where
                            bp.IsActive &&
                            b.IsActive &&
                            b.Batches_Statuss == 1 &&
                            bp.Version_Id == ptp.VersionId &&
                            bp.ProductToPart_ID == ptp.Id
                        select bp.Planned_Qty
                    ).Sum(),
                InProduction =
                    _db.BatchProducts
                        .Where(bp =>
                            bp.IsActive &&
                            bp.Version_Id == ptp.VersionId &&
                            bp.ProductToPart_ID == ptp.Id &&
                            tasksQuery.Any(x =>
                                x.t.BatchProduct_ID == bp.ID &&
                                x.ts.StepType == 1 &&
                                x.ts.IsFinal &&
                                (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3)
                            )
                            &&
                            tasksQuery.Any(x =>
                                x.t.BatchProduct_ID == bp.ID &&
                                x.ts.StepType == 1 &&
                                x.ts.IsFinal &&
                                x.t.Tasks_Status != 3
                            )
                        )
                        .Sum(bp => (int?)bp.Planned_Qty) ?? 0,
                
                OkQty =
                    _db.BatchProducts
                        .Where(bp =>
                            bp.IsActive &&
                            bp.Version_Id == ptp.VersionId &&
                            bp.ProductToPart_ID == ptp.Id &&

                            !tasksQuery
                                .Any(x =>
                                    x.t.BatchProduct_ID == bp.ID &&
                                    x.t.IsActive &&
                                    x.ts.StepType == 1 &&
                                    x.t.Tasks_Status != 3)
                        )
                        .Sum(bp => (int?)bp.Planned_Qty) ?? 0,

                ReservedQty =
                    tasksQuery
                        .Where(x =>
                            x.ts.StepType == 2 &&
                            (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3) &&
                            x.ts.ProductToPartId == ptp.Id)
                        .Sum(x => (int?)x.t.Qty_Done) ?? 0,
                FreeQty =
                        (
                            _db.BatchProducts
                                .Where(bp =>
                                    bp.IsActive &&
                                    bp.Version_Id == ptp.VersionId &&
                                    bp.ProductToPart_ID == ptp.Id)
                                .Sum(bp => (int?)bp.Planned_Qty) ?? 0
                        )
                        - (
                            tasksQuery
                                .Where(x =>
                                    x.ts.StepType == 2 &&
                                    (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3) &&
                                    x.ts.ProductToPartId == ptp.Id)
                                .Sum(x => (int?)x.t.Qty_Done) ?? 0
                        )
            }
    ).ToListAsync();

    return Ok(parts);
}


[HttpGet("full-data-all")]
public async Task<IActionResult> GetFullDataAll([FromQuery] string versionIds)
{
    if (string.IsNullOrWhiteSpace(versionIds))
        return BadRequest("versionIds required");

    var ids = versionIds
        .Split(',')
        .Select(int.Parse)
        .ToList();

    var tasksQuery =
        _db.Tasks
            .Join(_db.TopPartSteps,
                t => t.TopPartStep_ID,
                ts => ts.Id,
                (t, ts) => new { t, ts })
            .Where(x => x.t.IsActive);

    var data = await (
        from ptp in _db.ProductTopParts.AsNoTracking()
            .Where(ptp =>
                ids.Contains(ptp.VersionId)
                && ptp.IsActive
            )
        join tp in _db.TopParts on ptp.TopPartId equals tp.Id
        where tp.IsActive
            && tp.Stage == 1

        select new
        {
            ptp.VersionId,
            ptp.TopPartId,
            ProductToPartId = ptp.Id,
            tp.TopPartName,

            PlannedQty =
                (
                    from bp in _db.BatchProducts
                    join b in _db.Batches
                        on bp.Batch_Id equals b.ID
                    where
                        bp.IsActive &&
                        b.IsActive &&
                        b.Batches_Statuss == 1 &&
                        bp.Version_Id == ptp.VersionId &&
                        bp.ProductToPart_ID == ptp.Id
                    select (int?)bp.Planned_Qty
                ).Sum() ?? 0,

            InProduction =
                _db.BatchProducts
                    .Where(bp =>
                        bp.IsActive &&
                        bp.Version_Id == ptp.VersionId &&
                        bp.ProductToPart_ID == ptp.Id &&
                        tasksQuery.Any(x =>
                            x.t.BatchProduct_ID == bp.ID &&
                            x.ts.StepType == 1 &&
                            x.ts.IsFinal &&
                            (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3)
                        )
                        &&
                        tasksQuery.Any(x =>
                            x.t.BatchProduct_ID == bp.ID &&
                            x.ts.StepType == 1 &&
                            x.ts.IsFinal &&
                            x.t.Tasks_Status != 3
                        )
                    )
                    .Sum(bp => (int?)bp.Planned_Qty) ?? 0,

            OkQty =
                _db.BatchProducts
                    .Where(bp =>
                        bp.IsActive &&
                        bp.Version_Id == ptp.VersionId &&
                        bp.ProductToPart_ID == ptp.Id &&
                        !tasksQuery.Any(x =>
                            x.t.BatchProduct_ID == bp.ID &&
                            x.ts.StepType == 1 &&
                            x.t.Tasks_Status != 3)
                    )
                    .Sum(bp => (int?)bp.Planned_Qty) ?? 0,

            ReservedQty =
                tasksQuery
                    .Where(x =>
                        x.ts.StepType == 2 &&
                        (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3) &&
                        x.ts.ProductToPartId == ptp.Id)
                    .Sum(x => (int?)x.t.Qty_Done) ?? 0,

            FreeQty =
                (
                    _db.BatchProducts
                        .Where(bp =>
                            bp.IsActive &&
                            bp.Version_Id == ptp.VersionId &&
                            bp.ProductToPart_ID == ptp.Id)
                        .Sum(bp => (int?)bp.Planned_Qty) ?? 0
                )
                -
                (
                    tasksQuery
                        .Where(x =>
                            x.ts.StepType == 2 &&
                            (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3) &&
                            x.ts.ProductToPartId == ptp.Id)
                        .Sum(x => (int?)x.t.Qty_Done) ?? 0
                )
        }
    ).ToListAsync();

    return Ok(data);
}


[HttpGet("ral-summary-all")]
public async Task<IActionResult> GetRalSummaryAll(
    [FromQuery] string versionIds)
{
    if (string.IsNullOrWhiteSpace(versionIds))
        return BadRequest("versionIds required");

    var ids = versionIds
        .Split(',')
        .Select(int.Parse)
        .Distinct()
        .ToList();
    
    var partIds = await _db.ProductTopParts
    .Where(x =>
        ids.Contains(x.VersionId)
        && x.IsActive)
    .Select(x => x.Id)
    .ToListAsync();

    var orderRows = await (
        from oi in _db.OrderItems

        join map in _db.CustomerCodeMaps
            on oi.CustomerCodeMapId equals map.Id
        

        join ral in _db.RalColors
            on map.RalColorId equals ral.ID

        where
            oi.IsActive &&
            map.VersionId != null &&
            map.RalColorId != null &&
            map.TopPartId != null &&
            ids.Contains(map.VersionId.Value)

        group oi by new
            {
                map.VersionId,
                map.ProductToPartId,
                ral.Name
            }
            into g

            select new
            {
                VersionId = g.Key.VersionId!.Value,
                ProductToPartId = g.Key.ProductToPartId,
                RalCode = g.Key.Name,
                OrderQty = g.Sum(x => x.Quantity)
            }
    ).ToListAsync();

    return Ok(orderRows);
}

[HttpGet("workflow")]
public async Task<IActionResult> GetWorkflowTopParts()
{
    var rows = await _db.TopParts
            .Where(x => x.IsActive && x.Stage == 1)
            .OrderBy(x => x.TopPartName)
            .Select(x => new WorkflowTopPartDto
            {
                TopPartId = x.Id,
                TopPartName = x.TopPartName,
                TopPartCode = x.TopPartCode,
                Stage = x.Stage
            })
            .ToListAsync();

    return Ok(rows);
}

    [HttpGet("{topPartId:int}/usage")]
        public async Task<IActionResult> GetUsage(int topPartId)
            {
                var topPart = await _db.TopParts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == topPartId &&
                        x.IsActive);

                if (topPart == null)
                    return NotFound("TopPart nav atrasts.");

                if (topPart.TopPartType != TopPartType.Part)
                {
                    return Ok(new TopPartUsageDto());
                }

                var resultIds = new HashSet<int>();
                var visitedPartIds = new HashSet<int>();
                var queue = new Queue<int>();

                queue.Enqueue(topPartId);

                while (queue.Count > 0)
                {
                    var currentPartId = queue.Dequeue();

                    if (!visitedPartIds.Add(currentPartId))
                        continue;

                    var parentTopParts = await (
                        from component in _db.WorkflowComponents.AsNoTracking()

                        join workflow in _db.Workflows.AsNoTracking()
                            on component.WorkflowId equals workflow.Id

                        join parentTopPart in _db.TopParts.AsNoTracking()
                            on workflow.TopPartId equals (uint?)parentTopPart.Id

                        where
                            component.IsActive &&
                            component.ComponentType == 1 &&
                            component.TopPartId == currentPartId &&
                            workflow.IsActive &&
                            workflow.Status == WorkflowStatus.Released &&
                            workflow.IsCurrent &&
                            parentTopPart.IsActive

                        select new
                        {
                            parentTopPart.Id,
                            parentTopPart.TopPartType
                        }
                    )
                    .Distinct()
                    .ToListAsync();

                    foreach (var parent in parentTopParts)
                    {
                        if (parent.TopPartType == TopPartType.Part)
                        {
                            queue.Enqueue(parent.Id);
                        }
                        else if (parent.TopPartType == TopPartType.Product ||
                                parent.TopPartType == TopPartType.SparePart)
                        {
                            resultIds.Add(parent.Id);
                        }
                    }
                }

                var items = await _db.TopParts
                    .AsNoTracking()
                    .Where(x =>
                        resultIds.Contains(x.Id) &&
                        x.IsActive)
                    .OrderBy(x => x.TopPartName)
                    .Select(x => new TopPartUsageItemDto
                    {
                        TopPartId = x.Id,
                        TopPartCode = x.TopPartCode,
                        TopPartName = x.TopPartName,
                        TopPartType = (byte)x.TopPartType
                    })
                    .ToListAsync();

                return Ok(new TopPartUsageDto
                {
                    Count = items.Count,
                    Items = items
                });

            }


    }
}