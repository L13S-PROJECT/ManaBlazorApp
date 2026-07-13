// Workflowvalidators.cs

using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using ManiApi.DTOs.WorkFlow;

namespace ManiApi.Services.Workflow;

public class WorkflowValidator
{
    private readonly AppDbContext _db;

    public WorkflowValidator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WorkflowValidationResultDto> ValidateAsync(int workflowId)
    {
        var result = new WorkflowValidationResultDto();

        var workflow = await _db.Workflows
            .FirstOrDefaultAsync(x =>
                x.Id == workflowId &&
                x.IsActive);

        if (workflow == null)
            {
                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    Message = "Workflow nav atrasts."
                });

                result.IsValid = false;
                return result;
            }
        
        var workflowVersionId = workflow.VersionId;
        
        var nodes = await _db.WorkflowNodes
            .Where(x =>
                x.WorkflowId == workflowId &&
                x.IsActive)
            .ToListAsync();

        var connections = await _db.WorkflowNodeConnections
            .Where(x =>
                nodes.Select(n => n.Id).Contains(x.FromNodeId) ||
                nodes.Select(n => n.Id).Contains(x.ToNodeId))
            .ToListAsync();
        
        var productParts = await _db.ProductTopParts
            .Where(x =>
                x.VersionId == workflowVersionId &&
                x.IsActive)
            .ToListAsync();
        
        
        
        var finishNodes = nodes
            .Where(x => x.NodeType == 4)
            .ToList();

        var productFinishNode = FindProductFinish(
            nodes,
            connections);

        if (productFinishNode == null)
        {
            result.Errors.Add(new WorkflowValidationErrorDto
            {
                Message = "Workflow jābūt vienam produkta gala FINISH mezglam."
            });
        }

        var partNodes = nodes
            .Where(x => x.NodeType == 1)
            .ToList();

        ValidateTopPartSubParts(
            productParts,
            nodes,
            connections,
            result);
        
        ValidatePartFlows(
            partNodes,
            nodes,
            connections,
            result);
        
        var mergeNodes = nodes
            .Where(x => x.NodeType == 3)
            .ToList();
        
        // ValidateMergeNodes(
        //     mergeNodes,
        //     nodes,
        //     connections,
        //     result);

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private static void ValidateOrphanNodes(
            List<WorkflowNode> orphanNodes,
            WorkflowValidationResultDto result)
        {
            foreach (var node in orphanNodes)
            {
                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    NodeId = node.Id,
                    Message = $"Mezgls '{node.Name}' nav pieslēgts Workflow."
                });
            }
        }

    // private static void ValidateFinishNodes(
    //     List<WorkflowNode> finishNodes,
    //     List<WorkflowNodeConnection> connections,
    //     WorkflowValidationResultDto result)
    // {
    //     if (finishNodes.Count == 0)
    //     {
    //         result.Errors.Add(new WorkflowValidationErrorDto
    //         {
    //             Message = "Workflow nesatur nevienu FINISH mezglu."
    //         });

    //         return;
    //     }

    //     var productFinishNodes = finishNodes
    //         .Where(x => !connections.Any(c => c.FromNodeId == x.Id))
    //         .ToList();

    //     if (productFinishNodes.Count != 1)
    //     {
    //         result.Errors.Add(new WorkflowValidationErrorDto
    //         {
    //             Message = "Workflow jābūt vienam gala FINISH mezglam."
    //         });
    //     }
    // }

    private static void ValidateMergeNodes(
            List<WorkflowNode> mergeNodes,
            List<WorkflowNode> nodes,
            List<WorkflowNodeConnection> connections,
            WorkflowValidationResultDto result)
        {
            foreach (var merge in mergeNodes)
            {
                var previousNodes = GetPreviousNodeIds(merge.Id, connections);

                if (previousNodes.Count < 2)
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = merge.Id,
                        Message = "MERGE mezglam jābūt vismaz divām ieejām."
                    });
                }

                foreach (var previousNodeId in previousNodes)
                {
                    if (!IsFinishNode(previousNodeId, nodes))
                    {
                        result.Errors.Add(new WorkflowValidationErrorDto
                        {
                            NodeId = merge.Id,
                            Message = "MERGE drīkst pievienot tikai FINISH mezglus."
                        });

                        break;
                    }
                }

                var nextNodes = GetNextNodeIds(merge.Id, connections);

                if (nextNodes.Count != 1)
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = merge.Id,
                        Message = "MERGE mezglam jābūt tieši vienai izejai."
                    });
                }
            }
        }
    
    private static void ValidatePartFlows(
            List<WorkflowNode> partNodes,
            List<WorkflowNode> nodes,
            List<WorkflowNodeConnection> connections,
            WorkflowValidationResultDto result)
        {
            foreach (var part in partNodes)
            {
                if (!CanReachFinish(part.Id, nodes, connections, new HashSet<int>()))
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = part.Id,
                        Message = $"Detaļai '{part.Name}' nav sasniedzams FINISH."
                    });
                }

                if (HasCycle(
                        part.Id,
                        connections,
                        new HashSet<int>(),
                        new HashSet<int>()))
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = part.Id,
                        Message = $"Plūsmā '{part.Name}' atrasts ciklisks savienojums."
                    });
                }
            }
        }

    private static List<int> GetNextNodeIds(
        int nodeId,
        List<WorkflowNodeConnection> connections)
    {
        return connections
            .Where(x => x.FromNodeId == nodeId)
            .Select(x => x.ToNodeId)
            .ToList();
    }

    private static bool CanReachFinish(
            int nodeId,
            List<WorkflowNode> nodes,
            List<WorkflowNodeConnection> connections,
            HashSet<int> visited)
        {
            if (!visited.Add(nodeId))
                return false;

            var node = nodes.First(x => x.Id == nodeId);

            if (node.NodeType == 4)
                return true;

            foreach (var nextId in GetNextNodeIds(nodeId, connections))
            {
                if (CanReachFinish(nextId, nodes, connections, visited))
                    return true;
            }

            return false;
        }

    private static bool HasCycle(
        int nodeId,
        List<WorkflowNodeConnection> connections,
        HashSet<int> visited,
        HashSet<int> recursionStack)
    {
        if (recursionStack.Contains(nodeId))
            return true;

        if (!visited.Add(nodeId))
            return false;

        recursionStack.Add(nodeId);

        foreach (var nextId in GetNextNodeIds(nodeId, connections))
        {
            if (HasCycle(nextId, connections, visited, recursionStack))
                return true;
        }

        recursionStack.Remove(nodeId);

        return false;
    }

    private static List<int> GetPreviousNodeIds(
    int nodeId,
    List<WorkflowNodeConnection> connections)
        {
            return connections
                .Where(x => x.ToNodeId == nodeId)
                .Select(x => x.FromNodeId)
                .ToList();
        }

    private static bool IsFinishNode(
        int nodeId,
        List<WorkflowNode> nodes)
            {
                return nodes.Any(x =>
                    x.Id == nodeId &&
                    x.NodeType == 4);
            }

    private static void ValidateTopPartSubParts(
    List<ProductTopPart> productParts,
    List<WorkflowNode> nodes,
    List<WorkflowNodeConnection> connections,
    WorkflowValidationResultDto result)
    {
        
        var topParts = productParts
            .Where(x => x.ParentProductTopPartId == null)
            .ToList();

        foreach (var topPart in topParts)
            {
                var directSubParts = productParts
                    .Where(x => x.ParentProductTopPartId == topPart.Id)
                    .ToList();
                
                if (directSubParts.Count == 0)
                    continue;

                var topPartNode = nodes.FirstOrDefault(x =>
                    x.NodeType == 1 &&
                    x.ProductToPartId == topPart.Id);

                if (topPartNode == null)
                    continue;

                var topPartFinishNode = FindTopPartFinish(
                    topPartNode,
                    nodes,
                    connections);
                    
Console.WriteLine($"TOP PART = {topPartNode.Name}, FINISH = {topPartFinishNode?.Id}");

                if (topPartFinishNode == null)
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = topPartNode.Id,
                        Message = $"TOP PART '{topPartNode.Name}' nav atrasts gala FINISH."
                    });

                    continue;
                }

                if (topPart.AttachToNodeId == null)
                    {
                        result.Errors.Add(new WorkflowValidationErrorDto
                        {
                            NodeId = topPartNode?.Id,
                            Message = $"TOP PART '{topPartNode?.Name}' nav norādīts AttachToNodeId."
                        });

                        continue;
                    }

                
                var subPartNodes = nodes
                    .Where(x =>
                        x.NodeType == 1 &&
                        directSubParts.Any(p => p.Id == x.ProductToPartId))
                    .ToList();

                if (subPartNodes.Count != directSubParts.Count)
                    {
                        result.Errors.Add(new WorkflowValidationErrorDto
                        {
                            NodeId = topPartNode.Id,
                            Message = $"TOP PART '{topPartNode.Name}' satur SUB PART bez PART mezgla."
                        });

                        continue;
                    }

                foreach (var subPartNode in subPartNodes)
                    {
                        var subFinishNode = nodes.FirstOrDefault(x =>
                            x.NodeType == 4 &&
                            CanReachNode(subPartNode.Id, x.Id, connections));

                        var subMergeNodes = nodes
                            .Where(x =>
                                x.NodeType == 3 &&
                                connections.Any(c =>
                                    c.ToNodeId == x.Id &&
                                    c.FromNodeId == subFinishNode!.Id))
                            .ToList();
                        
                        if (subMergeNodes.Count == 0)
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subPartNode.Id,
                                    Message = $"SUB PART '{subPartNode.Name}' nav pievienots nevienam MERGE."
                                });

                                continue;
                            }

                        if (subMergeNodes.Count > 1)
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subPartNode.Id,
                                    Message = $"SUB PART '{subPartNode.Name}' ir pievienots vairākiem MERGE."
                                });

                                continue;
                            }

                        var subMergeNode = subMergeNodes.Single();

                        var parentPartNode = FindParentPartNode(
                            subMergeNode,
                            nodes,
                            connections);

                        if (parentPartNode == null)
                        {
                            result.Errors.Add(new WorkflowValidationErrorDto
                            {
                                NodeId = subMergeNode.Id,
                                Message = "MERGE nav iespējams sasaistīt ar nevienu PART."
                            });

                            continue;
                        }

                        if (parentPartNode.ProductToPartId != topPartNode.ProductToPartId)
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subPartNode.Id,
                                    Message = $"SUB PART '{subPartNode.Name}' ir pievienots nepareizai TOP PART plūsmai."
                                });

                                continue;
                            }

                        var nextNodes = GetNextNodeIds(subMergeNode.Id, connections);

                        if (!CanReachNode(subMergeNode.Id, topPartFinishNode.Id, connections))
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subMergeNode.Id,
                                    Message = $"MERGE nenonāk līdz TOP PART '{topPartNode.Name}' gala FINISH."
                                });

                                continue;
                            }
                        
                        if (!CanReachNode(topPartNode.Id, subMergeNode.Id, connections))
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subMergeNode.Id,
                                    Message = $"MERGE neatrodas TOP PART '{topPartNode.Name}' plūsmā."
                                });

                                continue;
                            }

                        if (nextNodes.Count != 1)
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subMergeNode.Id,
                                    Message = "MERGE mezglam jābūt tieši vienai izejai."
                                });

                                continue;
                            }
                          

                        if (subFinishNode == null)
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subPartNode.Id,
                                    Message = $"SUB PART '{subPartNode.Name}' nav sasniedzams FINISH."
                                });

                                continue;
                            }
                    }                
            }
    }

    private static bool CanReachNode(
            int fromNodeId,
            int targetNodeId,
            List<WorkflowNodeConnection> connections,
            HashSet<int>? visited = null)
        {
            visited ??= new HashSet<int>();

            if (!visited.Add(fromNodeId))
                return false;

            if (fromNodeId == targetNodeId)
                return true;

            foreach (var nextId in GetNextNodeIds(fromNodeId, connections))
            {
                if (CanReachNode(nextId, targetNodeId, connections, visited))
                    return true;
            }

            return false;
        }

    private static WorkflowNode? FindParentPartNode(
        WorkflowNode mergeNode,
        List<WorkflowNode> nodes,
        List<WorkflowNodeConnection> connections)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        queue.Enqueue(mergeNode.Id);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            if (!visited.Add(currentId))
                continue;

            var previousIds = GetPreviousNodeIds(currentId, connections);

            foreach (var previousId in previousIds)
            {
                var node = nodes.First(x => x.Id == previousId);

                if (node.NodeType == 1)
                    return node;

                queue.Enqueue(previousId);
            }
        }

        return null;
    }

        private static bool IsNodeBetween(
            int startNodeId,
            int middleNodeId,
            int endNodeId,
            List<WorkflowNodeConnection> connections)
        {
            return CanReachNode(startNodeId, middleNodeId, connections)
                && CanReachNode(middleNodeId, endNodeId, connections);
        }

        private static WorkflowNode? FindTopPartFinish(
            WorkflowNode topPartNode,
            List<WorkflowNode> nodes,
            List<WorkflowNodeConnection> connections)
        {
            var visited = new HashSet<int>();

            return FindTopPartFinishRecursive(
                topPartNode.Id,
                nodes,
                connections,
                visited);
        }

        private static WorkflowNode? FindTopPartFinishRecursive(
                int nodeId,
                List<WorkflowNode> nodes,
                List<WorkflowNodeConnection> connections,
                HashSet<int> visited)
            {
                if (!visited.Add(nodeId))
                    return null;

                var currentNode = nodes.First(x => x.Id == nodeId);

                if (currentNode.NodeType == 4)
                {
                    var nextNodeIds = GetNextNodeIds(currentNode.Id, connections);

                    if (nextNodeIds.Count == 0)
                        return currentNode;

                    if (nextNodeIds.All(id =>
                        nodes.First(x => x.Id == id).NodeType == 3))
                    {
                        return currentNode;
                    }
                }


               foreach (var nextNodeId in GetNextNodeIds(nodeId, connections))
                {
                    var finish = FindTopPartFinishRecursive(
                        nextNodeId,
                        nodes,
                        connections,
                        visited);

                    if (finish != null)
                        return finish;
                }

                return null;
                
            }

        private static WorkflowNode? FindProductFinish(
                List<WorkflowNode> nodes,
                List<WorkflowNodeConnection> connections)
            {
                var productFinishNodes = nodes
                    .Where(x =>
                        x.NodeType == 4 &&
                        !connections.Any(c => c.FromNodeId == x.Id))
                    .ToList();

                if (productFinishNodes.Count != 1)
                    return null;

                return productFinishNodes.Single();
            }

}