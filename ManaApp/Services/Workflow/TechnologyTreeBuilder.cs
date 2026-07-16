using ManaApp.Models;
using ManaApp.ViewModels.Workflow;

namespace ManaApp.Services.Workflow;

public class TechnologyTreeBuilder
{
    private readonly WorkflowState _state;

    public TechnologyTreeBuilder(WorkflowState state)
    {
        _state = state;
    }

    public List<TechnologyTreeItem> Build()
        {
            var result = new List<TechnologyTreeItem>();

            var roots = _state.ProductParts
                .Where(x => x.ParentProductTopPartId == null)
                .OrderBy(x => x.TopPartName);

            foreach (var part in roots)
                {
                    var item = CreateRootPart(part);

                    var startNode = FindPartNode(part);

                    if (startNode != null)
                    {
                        BuildFlow(startNode, item.NodeChildren);
                    }

                    result.Add(item);
                }

            return result;
        }
    
    private TechnologyTreeItem CreateRootPart(WorkflowPartModel part)
        {
            var item = new TechnologyTreeItem
            {
                Part = part,
                Node = FindPartNode(part)?.Node,
                PartChildren = new(),
                NodeChildren = new(),
                Level = 0
            };

            return item;
        }

    private WorkflowGraphNode? FindPartNode(WorkflowPartModel part)
        {
            return _state.PartNodeByProductToPartId.TryGetValue(
                part.ProductToPartId,
                out var node)
                    ? node
                    : null;
        }

    private void BuildFlow(
        WorkflowGraphNode current,
        List<TechnologyTreeItem> items)
    {
        foreach (var next in current.Next.OrderBy(x => x.Node.SortOrder))
        {
            var item = new TechnologyTreeItem
            {
                Node = next.Node,
                PartChildren = new(),
                NodeChildren = new(),
                IsFlowChild = true
            };

            items.Add(item);

            BuildFlow(next, item.NodeChildren);
        }
    }

}