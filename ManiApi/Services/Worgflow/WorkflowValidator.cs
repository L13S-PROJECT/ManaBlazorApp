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
        
        // var productFinishNode = analyzer.GetProductFinishNode();
        
        // if (productFinishNode == null)
        // {
        //     result.Errors.Add(new WorkflowValidationErrorDto
        //     {
        //         Message = "Workflow jābūt vienam produkta gala FINISH mezglam."
        //     });
        // }

        var flowOwners = analyzer.GetFlowOwnerNodes().ToList();

        ValidateTopPartSubParts(
            analyzer,
            productParts,
            result);
        
        ValidatePartFlows(
            analyzer,
            flowOwners,
            result);
        
        var mergeNodes = analyzer.GetMergeNodes().ToList();
        
        ValidateMergeNodes(
            analyzer,
            mergeNodes,
            result);

        ValidateProductWorkflow(analyzer, result);

        result.IsValid = result.Errors.Count == 0;
        
        return result;
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

                if (!previousNodes.All(analyzer.IsFinishNode))
                    {
                        result.Errors.Add(new WorkflowValidationErrorDto
                        {
                            NodeId = merge.Id,
                            Message = "MERGE drīkst pievienot tikai FINISH mezglus."
                        });
                    }

                var nextNodes = analyzer.GetNextNodeIds(merge.Id);

                if (nextNodes.Count != 1)
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = merge.Id,
                        Message = "MERGE mezglam jābūt tieši vienai izejai."
                    });

                    continue;
                }

                

                if (analyzer.IsFinishNode(nextNodes[0]))
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = merge.Id,
                        Message = "Pēc MERGE jāseko PROCESS vai MERGE. FINISH nedrīkst būt nākamais mezgls."
                    });
                }
            }
        }
    
    private static void ValidatePartFlows(
        WorkflowFlowAnalyzer analyzer,
        List<WorkflowNode> flowOwners,
        WorkflowValidationResultDto result)
        {
            foreach (var owner in flowOwners)
            {
               if (analyzer.GetFlowFinishNode(owner) == null)
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = owner.Id,
                        Message = $"Flow '{owner.Name}' nav sasniedzams FINISH."
                    });
                }

                var visited = new HashSet<int>();
                var recursionStack = new HashSet<int>();        

                 if (analyzer.HasCycle(
                    owner.Id,
                    visited,
                    recursionStack))
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = owner.Id,
                        Message = $"Plūsmā '{owner.Name}' atrasts ciklisks savienojums."
                    });
                }
            }
        }

    private static List<ProductTopPart> GetTopParts(
    List<ProductTopPart> productParts)
        {
            return productParts
                .Where(x => x.ParentProductTopPartId == null)
                .ToList();
        }

    private static List<ProductTopPart> GetDirectSubParts(
    List<ProductTopPart> productParts,
    ProductTopPart topPart)
        {
            return productParts
                .Where(x => x.ParentProductTopPartId == topPart.Id)
                .ToList();
        }

    private static WorkflowNode? GetValidatedTopPartNode(
            WorkflowFlowAnalyzer analyzer,
            ProductTopPart topPart,
            WorkflowValidationResultDto result)
        {
            var topPartNode = analyzer.GetPartNode(topPart.Id);

            if (topPartNode != null)
                return topPartNode;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                Message = $"TOP PART ar ID {topPart.Id} nav atrasts PART mezgls."
            });

            return null;
        }

    private static WorkflowNode? GetValidatedTopPartFinish(
            WorkflowFlowAnalyzer analyzer,
            WorkflowNode topPartNode,
            WorkflowValidationResultDto result)
        {
            var finishNode = analyzer.GetFlowFinishNode(topPartNode);

            if (finishNode != null)
                return finishNode;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                NodeId = topPartNode.Id,
                Message = $"TOP PART '{topPartNode.Name}' nav atrasts gala FINISH."
            });

            return null;
        }

        private static WorkflowNode? GetValidatedSubMergeNode(
                WorkflowFlowAnalyzer analyzer,
                WorkflowNode subFinishNode,
                WorkflowNode subPartNode,
                WorkflowValidationResultDto result)
            {
                var subMergeNodes = analyzer.GetNextMergeNodes(subFinishNode.Id);

                if (subMergeNodes.Count == 1)
                    return subMergeNodes[0];

                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    NodeId = subPartNode.Id,
                    Message = subMergeNodes.Count == 0
                        ? $"SUB PART '{subPartNode.Name}' nav pievienots nevienam MERGE."
                        : $"SUB PART '{subPartNode.Name}' ir pievienots vairākiem MERGE."
                });

                return null;
            }

        private static WorkflowNode? GetValidatedParentPartNode(
            WorkflowFlowAnalyzer analyzer,
            WorkflowNode subMergeNode,
            WorkflowValidationResultDto result)
        {
            var parentPartNode = analyzer.FindParentPartNode(subMergeNode);

            if (parentPartNode != null)
                return parentPartNode;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                NodeId = subMergeNode.Id,
                Message = "MERGE nav iespējams sasaistīt ar nevienu PART."
            });

            return null;
        }

        private static bool ValidateParentFlow(
            WorkflowNode parentPartNode,
            WorkflowNode topPartNode,
            WorkflowNode subPartNode,
            WorkflowValidationResultDto result)
        {
            if (parentPartNode.ProductToPartId == topPartNode.ProductToPartId)
                return true;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                NodeId = subPartNode.Id,
                Message = $"SUB PART '{subPartNode.Name}' ir pievienots nepareizai TOP PART plūsmai."
            });

            return false;
        }

        private static bool ValidateMergeFlow(
                WorkflowFlowAnalyzer analyzer,
                WorkflowNode subMergeNode,
                WorkflowNode topPartNode,
                WorkflowNode topPartFinishNode,
                WorkflowValidationResultDto result)
            {
                if (!analyzer.CanReachNode(subMergeNode.Id, topPartFinishNode.Id))
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = subMergeNode.Id,
                        Message = $"MERGE nenonāk līdz TOP PART '{topPartNode.Name}' gala FINISH."
                    });

                    return false;
                }

                if (!analyzer.CanReachNode(topPartNode.Id, subMergeNode.Id))
                {
                    result.Errors.Add(new WorkflowValidationErrorDto
                    {
                        NodeId = subMergeNode.Id,
                        Message = $"MERGE neatrodas TOP PART '{topPartNode.Name}' plūsmā."
                    });

                    return false;
                }

                return true;
            }

        private static bool ValidateMergeOutput(
            WorkflowFlowAnalyzer analyzer,
            WorkflowNode subMergeNode,
            WorkflowValidationResultDto result)
        {
            var nextNodes = analyzer.GetNextNodeIds(subMergeNode.Id);

            if (nextNodes.Count != 1)
            {
                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    NodeId = subMergeNode.Id,
                    Message = "MERGE mezglam jābūt tieši vienai izejai."
                });

                return false;
            }

            if (analyzer.IsFinishNode(nextNodes[0]))
            {
                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    NodeId = subMergeNode.Id,
                    Message = "Pēc MERGE jāseko PROCESS vai MERGE. FINISH nedrīkst būt nākamais mezgls."
                });

                return false;
            }

            return true;
        }

        private static void ValidateDirectSubParts(
            WorkflowFlowAnalyzer analyzer,
            List<ProductTopPart> directSubParts,
            WorkflowNode topPartNode,
            WorkflowNode topPartFinishNode,
            WorkflowValidationResultDto result)
            {
                foreach (var subPart in directSubParts)
                    {
                        var subPartNode = GetValidatedSubPartNode(analyzer, subPart, result);

                        if (subPartNode == null)
                            continue;
                        
                        var subFinishNode = GetValidatedSubPartFinish(
                            analyzer,
                            subPartNode,
                            result);

                        if (subFinishNode == null)
                            continue;

                        var subFinishNodeId = subFinishNode.Id;

                        var subMergeNode = GetValidatedSubMergeNode(
                            analyzer,
                            subFinishNode,
                            subPartNode,
                            result);

                        if (subMergeNode == null)
                            continue;

                        var parentPartNode = GetValidatedParentPartNode(
                            analyzer,
                            subMergeNode,
                            result);

                        if (parentPartNode == null)
                            continue;

                        if (!ValidateParentFlow(
                            parentPartNode,
                            topPartNode,
                            subPartNode,
                            result))
                        {
                            continue;
                        }

                        if (!ValidateMergeFlow(
                            analyzer,
                            subMergeNode,
                            topPartNode,
                            topPartFinishNode,
                            result))
                        {
                            continue;
                        }

                        if (!ValidateMergeOutput(
                                analyzer,
                                subMergeNode,
                                result))
                            {
                                continue;
                            }
                    }  
            }

    private static WorkflowNode? GetValidatedSubPartNode(
            WorkflowFlowAnalyzer analyzer,
            ProductTopPart subPart,
            WorkflowValidationResultDto result)
        {
            var subPartNode = analyzer.GetPartNode(subPart.Id);

            if (subPartNode != null)
                return subPartNode;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                Message = $"SUB PART ar ID {subPart.Id} nav atrasts PART mezgls."
            });

            return null;
        }

    private static WorkflowNode? GetValidatedSubPartFinish(
            WorkflowFlowAnalyzer analyzer,
            WorkflowNode subPartNode,
            WorkflowValidationResultDto result)
        {
            var subFinishNode = analyzer.GetFlowFinishNode(subPartNode);

            if (subFinishNode != null)
                return subFinishNode;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                NodeId = subPartNode.Id,
                Message = $"SUB PART '{subPartNode.Name}' nav sasniedzams FINISH."
            });

            return null;
        }

        private static bool ValidateTopPartMerge(
                WorkflowFlowAnalyzer analyzer,
                WorkflowNode topPartNode,
                WorkflowNode topPartFinishNode,
                WorkflowValidationResultDto result)
            {
                var mergeExists = analyzer
                    .GetNextMergeNodes(topPartFinishNode.Id)
                    .Any();

                if (mergeExists)
                    return true;

                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    NodeId = topPartNode.Id,
                    Message = $"TOP PART '{topPartNode.Name}' gala FINISH nav pievienots MERGE."
                });

                return false;
            }


        private static bool ValidateAttachNode(
            ProductTopPart topPart,
            WorkflowNode topPartNode,
            WorkflowValidationResultDto result)
        {
            if (topPart.AttachToNodeId != null)
                return true;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                NodeId = topPartNode.Id,
                Message = $"TOP PART '{topPartNode.Name}' nav norādīts AttachToNodeId."
            });

            return false;
        }


        private static bool ValidateSubPartNodes(
            WorkflowFlowAnalyzer analyzer,
            List<ProductTopPart> directSubParts,
            WorkflowNode topPartNode,
            WorkflowValidationResultDto result)
        {
            if (!directSubParts.Any(x => analyzer.GetPartNode(x.Id) == null))
                return true;

            result.Errors.Add(new WorkflowValidationErrorDto
            {
                NodeId = topPartNode.Id,
                Message = $"TOP PART '{topPartNode.Name}' satur SUB PART bez PART mezgla."
            });

            return false;
        }

    private static void ValidateTopPart(
    WorkflowFlowAnalyzer analyzer,
    ProductTopPart topPart,
    List<ProductTopPart> productParts,
    WorkflowValidationResultDto result)
        {
            var directSubParts = GetDirectSubParts(productParts, topPart);

            if (!directSubParts.Any())
                return;

            var topPartNode = GetValidatedTopPartNode(
                analyzer,
                topPart,
                result);

            if (topPartNode == null)
                return;

            var topPartFinishNode = GetValidatedTopPartFinish(
                analyzer,
                topPartNode,
                result);

            if (topPartFinishNode == null)
                return;

            if (!ValidateTopPartMerge(
                analyzer,
                topPartNode,
                topPartFinishNode,
                result))
                return;

            if (!ValidateAttachNode(
                topPart,
                topPartNode,
                result))
                return;

            if (!ValidateSubPartNodes(
                analyzer,
                directSubParts,
                topPartNode,
                result))
                return;

            ValidateDirectSubParts(
                analyzer,
                directSubParts,
                topPartNode,
                topPartFinishNode,
                result);
        }

    private static void ValidateTopPartSubParts(
    WorkflowFlowAnalyzer analyzer,
    List<ProductTopPart> productParts,
    WorkflowValidationResultDto result)
    {
        
        var topParts = GetTopParts(productParts);

        if (!ShouldValidateTopPartSubParts(topParts))
            return;

        foreach (var topPart in topParts)
            {
                ValidateTopPart(
                    analyzer,
                    topPart,
                    productParts,
                    result);
            }
            
    }

    private static bool ShouldValidateTopPartSubParts(
            List<ProductTopPart> topParts)
        {
            return topParts.Any();
        }

    private static void ValidateProductWorkflow(
    WorkflowFlowAnalyzer analyzer,
    WorkflowValidationResultDto result)
    {
        var productFinishNode = analyzer.GetProductFinishNode();

        if (productFinishNode == null)
            {
                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    Message = "Workflow jābūt vienam produkta gala FINISH mezglam."
                });

                return;
            }
        
        var unconsumedFlowFinishes = analyzer.GetUnconsumedFlowFinishes();
        

            if (unconsumedFlowFinishes.Count != 1)
            {
                result.Errors.Add(new WorkflowValidationErrorDto
                {
                    Message = "Workflow jābeidzas ar vienu kopīgu gala Flow."
                });

                return;
            }

    }


}