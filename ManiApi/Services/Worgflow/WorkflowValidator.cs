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
        
        var analyzer = new WorkflowFlowAnalyzer(
            nodes,
            connections,
            productParts);
        
        var productFinishNode = analyzer.GetProductFinishNode();
        
        var finishNodes = nodes
            .Where(x => x.NodeType == 4)
            .ToList();

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
            analyzer,
            productParts,
            nodes,
            connections,
            result);
        
        ValidatePartFlows(
            analyzer,
            partNodes,
            nodes,
            connections,
            result);
        
        var mergeNodes = nodes
            .Where(x => x.NodeType == 3)
            .ToList();
        
        ValidateMergeNodes(
            analyzer,
            mergeNodes,
            nodes,
            connections,
            result);

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
            WorkflowFlowAnalyzer analyzer,
            List<WorkflowNode> mergeNodes,
            List<WorkflowNode> nodes,
            List<WorkflowNodeConnection> connections,
            WorkflowValidationResultDto result)
        {
            foreach (var merge in mergeNodes)
            {
                var previousNodes = analyzer.GetPreviousNodeIds(merge.Id);

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
                   if (!analyzer.IsFinishNode(previousNodeId))
                    {
                        result.Errors.Add(new WorkflowValidationErrorDto
                        {
                            NodeId = merge.Id,
                            Message = "MERGE drīkst pievienot tikai FINISH mezglus."
                        });

                        break;
                    }
                }

                var nextNodes = analyzer.GetNextNodeIds(merge.Id);

                if (nextNodes.Count != 1)
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = merge.Id,
                        Message = "MERGE mezglam jābūt tieši vienai izejai."
                    });
                }

                if (nextNodes.Count == 1)
                    {
                        var nextNode = analyzer.GetNode(nextNodes[0])!;

                        if (analyzer.IsFinishNode(nextNode.Id))
                        {
                            result.Errors.Add(new WorkflowValidationErrorDto
                            {
                                NodeId = merge.Id,
                                Message = "Pēc MERGE jāseko PROCESS vai MERGE. FINISH nedrīkst būt nākamais mezgls."
                            });
                        }
                    }
            }
        }
    
    private static void ValidatePartFlows(
        WorkflowFlowAnalyzer analyzer,
        List<WorkflowNode> partNodes,
        List<WorkflowNode> nodes,
        List<WorkflowNodeConnection> connections,
        WorkflowValidationResultDto result)
        {
            foreach (var part in partNodes)
            {
                if (!analyzer.HasFlowFinish(part.Id))
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = part.Id,
                        Message = $"Detaļai '{part.Name}' nav sasniedzams FINISH."
                    });
                }

                 if (analyzer.HasCycle(
                    part.Id,
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


    private static void ValidateTopPartSubParts(
            WorkflowFlowAnalyzer analyzer,
            List<ProductTopPart> productParts,
            List<WorkflowNode> nodes,
            List<WorkflowNodeConnection> connections,
            WorkflowValidationResultDto result)
    {
        
        var topParts = productParts
            .Where(x => x.ParentProductTopPartId == null)
            .ToList();
        
        if (topParts.Count < 2)
            return;

        if (topParts.Count < 2)
            {
                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    Message = "Workflow nav pabeigts!"
                });

                return;
            }

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

                var topPartFinishNode = analyzer.FindTopPartFinish(topPartNode);

                var mergeExists = connections.Any(c =>
                    c.FromNodeId == topPartFinishNode?.Id &&
                    nodes.Any(n =>
                        n.Id == c.ToNodeId &&
                        n.NodeType == 3));

                if (!mergeExists)
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = topPartNode.Id,
                        Message = $"TOP PART '{topPartNode.Name}' gala FINISH nav pievienots MERGE."
                    });

                    continue;
                } 

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
                            analyzer.CanReachNode(subPartNode.Id, x.Id));

                        if (subFinishNode == null)
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subPartNode.Id,
                                    Message = $"SUB PART '{subPartNode.Name}' nav sasniedzams FINISH."
                                });

                                continue;
                            }

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

                        var parentPartNode = analyzer.FindParentPartNode(
                            subMergeNode);

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

                        var nextNodes = analyzer.GetNextNodeIds(subMergeNode.Id);

                        if (!analyzer.CanReachNode(subMergeNode.Id, topPartFinishNode.Id))
                            {
                                result.Errors.Add(new WorkflowValidationErrorDto
                                {
                                    NodeId = subMergeNode.Id,
                                    Message = $"MERGE nenonāk līdz TOP PART '{topPartNode.Name}' gala FINISH."
                                });

                                continue;
                            }
                        
                        if (!analyzer.CanReachNode(topPartNode.Id, subMergeNode.Id))
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
                    }                
            }
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