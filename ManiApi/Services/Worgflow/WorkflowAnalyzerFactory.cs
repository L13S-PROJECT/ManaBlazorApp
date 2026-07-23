using Microsoft.EntityFrameworkCore;
using ManiApi.Data;

namespace ManiApi.Services.Workflow;

public class WorkflowAnalyzerFactory
{
    private readonly AppDbContext _db;

    public WorkflowAnalyzerFactory(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WorkflowFlowAnalyzer> CreateAsync(int workflowId)
    {
        var workflow = await _db.Workflows
            .FirstAsync(x => x.Id == workflowId && x.IsActive);

        var workflowNodes = await _db.WorkflowNodes
            .Where(x => x.WorkflowId == workflowId && x.IsActive)
            .ToListAsync();

        var nodeIds = workflowNodes
            .Select(x => x.Id)
            .ToList();

        var connections = await _db.WorkflowNodeConnections
            .Where(x =>
                nodeIds.Contains(x.FromNodeId) ||
                nodeIds.Contains(x.ToNodeId))
            .ToListAsync();

        var productParts = await _db.ProductTopParts
            .Where(x =>
                x.VersionId == workflow.VersionId &&
                x.IsActive)
            .ToListAsync();
        
        var dependencies = await _db.WorkflowDependencies
            .Where(x => x.WorkflowId == workflowId)
            .ToListAsync();

        return new WorkflowFlowAnalyzer(
            workflowNodes,
            connections,
            dependencies);
    }

    
}