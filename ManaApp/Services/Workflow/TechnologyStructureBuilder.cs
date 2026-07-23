// technologyStrucyureBuilder.cs

using ManaApp.Models;
using ManaApp.ViewModels.Workflow;
using ManaApp.DTOs.Workflow;

namespace ManaApp.Services.Workflow;

public class TechnologyStructureBuilder
{
    private readonly WorkflowState _state;
    private readonly List<AvailableFlowDto> _flows;
    public TechnologyStructureBuilder(WorkflowState state)
        {
            _state = state;
            _flows = state.AvailableFlows
                .Select(x => x.Flow)
                .ToList();
        }

    public List<TechnologyStructureItem> Build()
    {
       
Console.WriteLine("=== TECHNOLOGY STRUCTURE BUILDER ===");

        var result = new List<TechnologyStructureItem>();

        var topParts = _state.ProductParts
            .Where(x => x.ParentProductTopPartId == null)
            .OrderBy(x => x.TopPartName);
Console.WriteLine($"TOP PART COUNT = {topParts.Count()}");

        foreach (var part in topParts)
        {
            result.Add(CreatePart(part));
            Console.WriteLine($"TOP PART -> {part.TopPartName}");
        }

        return result;
    }

    private TechnologyStructureItem CreatePart(
        WorkflowPartModel part,
        int flowLevel = 0)

        {
            var startNode = FindGraphPartNode(part);

            var hasValidationError =
                startNode != null &&
                _state.InvalidFlowOwnerNodeIds.Contains(startNode.Node.Id);

            var flow = FindFlow(part);

            var item = new TechnologyStructureItem
                {
                    Part = part,
                    Node = startNode?.Node,
                    Flow = flow,
                    FlowLevel = flowLevel + 1,
                    HasValidationError = hasValidationError
                    
                };
Console.WriteLine($"PART {part.TopPartName} HasValidation={hasValidationError}");

            if (startNode != null)
                {
                    BuildFlow(startNode, item, item.Children);
                }

            return item;
        }
    

    // TODO:
    // Šī metode izmanto UI Graph.
    // Pakāpeniski aizstāt ar WorkflowFlowAnalyzer.GetPartNode().

    private WorkflowGraphNode? FindGraphPartNode(WorkflowPartModel part)
        {
            return _state.PartNodeByProductToPartId.TryGetValue(
                part.ProductToPartId,
                out var node)
                    ? node
                    : null;
        }

    private TechnologyStructureItem CreateNode(
            WorkflowGraphNode node,
            int flowLevel)
        {
                      
            return new TechnologyStructureItem
                {
                    Node = node.Node,
                    FlowLevel = flowLevel,
                    HasValidationError =
                        (node.Node.NodeType == 3 || node.Node.NodeType == 1) &&
                        _state.InvalidFlowOwnerNodeIds.Contains(node.Node.Id)
                };
        }

    private TechnologyStructureItem AddFlowNode(
        TechnologyStructureItem parent,
        List<TechnologyStructureItem> items,
        WorkflowGraphNode node)
    {
       
       var item = CreateNode(node, node.Node.NodeType == 1 ? 0 : parent.FlowLevel);


        items.Add(item);

        return item;
    }

    private void AttachSubParts(
            WorkflowGraphNode node,
            TechnologyStructureItem parent)
        {
               
            var attachedParts = _state.ProductParts
                .Where(x =>
                    x.AttachToNodeId == node.Node.Id)
                .OrderBy(x => x.TopPartName);



            foreach (var part in attachedParts)
                {
                    var child = CreatePart(part, parent.FlowLevel + 1);

                    parent.Children.Add(child);
                    child.Parent = parent;
                }
        }
    
    private bool HasSubParts(WorkflowGraphNode node)
        {
            return _state.ProductParts.Any(x =>
                x.ParentProductTopPartId == node.Node.ProductToPartId &&
                x.AttachToNodeId == node.Node.Id);
        }

    private List<WorkflowPartModel> GetAttachedParts(WorkflowGraphNode node)
        {
            return _state.ProductParts
                .Where(x =>
                    x.AttachToNodeId == node.Node.Id)
                .OrderBy(x => x.TopPartName)
                .ToList();
        }    

    private void BuildSubParts(
        TechnologyStructureItem item)
        {
            if (item.Node == null)
                return;

            if (!_state.Graph.TryGetValue(item.Node.Id, out var node))
                return;
                
            var attachedParts = GetAttachedParts(node);

            if (attachedParts.Count == 0)
                return;

            AttachSubParts(node, item);

        }

    private void BuildFlow(
    WorkflowGraphNode current,
    TechnologyStructureItem parent,
    List<TechnologyStructureItem> items)
            {
                BuildSubParts(parent);
                
                foreach (var next in current.Next.OrderBy(x => x.Node.SortOrder))
                {

                    var target = AddFlowNode(parent, items, next);

                    target.Parent = parent;

                    items = target.Children;

                    BuildFlow(next, target, items);
                }
            }

    private AvailableFlowDto? FindFlow(WorkflowPartModel part)
        {
            return _flows.FirstOrDefault(x =>
                x.OwnerProductToPartId == part.ProductToPartId);
        }

    

}