using ManaApp.Models;
using ManaApp.ViewModels.Workflow;

namespace ManaApp.Services.Workflow;

public class WorkflowGraphMapper
{
    private readonly WorkflowState _state;
    private readonly Dictionary<int, WorkflowGraphItem> _cache = new();
    public WorkflowGraphMapper(WorkflowState state)
    {
        _state = state;
    }

    public List<WorkflowGraphItem> Map(
            IEnumerable<WorkflowGraphNode> nodes)
        {
            var result = nodes
                .Select(MapNode)
                .ToList();

            foreach (var item in result.Where(x => x.IsRoot))
                {
                    BuildDependencies(item);
                    BuildExplorerHierarchy(item);
                    BuildFlowNodes(item);
                }

            return result;
        }

    private WorkflowGraphItem MapNode(
    WorkflowGraphNode graphNode)
{
    if (_cache.TryGetValue(graphNode.Node.Id, out var existing))
        {
            return existing;
        }

        var item = new WorkflowGraphItem
    {
       
        Node = graphNode.Node,

        Part = graphNode.Node.ProductToPartId.HasValue &&
            _state.ProductParts
                .FirstOrDefault(x =>
                    x.ProductToPartId == graphNode.Node.ProductToPartId)
                is var part
            ? part
            : null,

        Flow = _state.AvailableFlows
            .Select(x => x.Flow)
            .FirstOrDefault(x =>
                x.StartNodeId == graphNode.Node.Id)
    };

    _cache[graphNode.Node.Id] = item;

    item.IsRoot = graphNode.Previous.Count == 0;

        foreach (var nextNode in graphNode.Next)
            {
                var nextItem = MapNode(nextNode);

                if (!item.NextNodes.Contains(nextItem))
                {
                    item.NextNodes.Add(nextItem);
                }

                if (!nextItem.PreviousNodes.Contains(item))
                {
                    nextItem.PreviousNodes.Add(item);
                }
            }
        
        

        return item;
    }

   private void BuildDependencies(
        WorkflowGraphItem item,
        HashSet<WorkflowGraphItem>? visited = null)
    {
        visited ??= new HashSet<WorkflowGraphItem>();

        if (!visited.Add(item))
            return;

        foreach (var next in item.NextNodes)
        {
            if (next.Node?.NodeType == 1 &&
                next.Node.Id != item.Node?.Id)
            {
                next.IsDependency = true;
            }

            BuildDependencies(next, visited);
        }
    }

    private void BuildExplorerHierarchy(
        WorkflowGraphItem item,
        int level = 0,
        HashSet<WorkflowGraphItem>? visited = null)
        {
            visited ??= new HashSet<WorkflowGraphItem>();

            if (!visited.Add(item))
                return;

            item.Level = level;

            item.IsSubPart =
                item.Part?.ParentProductTopPartId != null;

            foreach (var next in item.NextNodes)
            {
                BuildExplorerHierarchy(next, level + 1, visited);
            }
        }

    private void BuildFlowNodes(WorkflowGraphItem root)
        {
            root.FlowNodes.Clear();

            var current = root;

            while (current != null)
            {
                root.FlowNodes.Add(current);

                current = current.NextNodes.FirstOrDefault(x =>
                    x.Node?.NodeType == 2 ||
                    x.Node?.NodeType == 4);
            }

Console.WriteLine(
    $"{root.Node?.Name} => {string.Join(" -> ",
        root.FlowNodes.Select(x => $"{x.Node?.NodeType}:{x.Node?.Name}"))}");

        }


}