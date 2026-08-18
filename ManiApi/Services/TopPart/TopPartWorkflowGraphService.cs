using ManaApp.Shared.DTOs.TopPart;

namespace ManiApi.Services.TopParts
{
    public class TopPartWorkflowGraphService
    {
        public void CalculateLayout(
            List<TopPartWorkflowNodeDto> nodes,
            List<TopPartWorkflowConnectionDto> connections)
        {

            var rootNodes = nodes
                .Where(x => x.NodeType == 1)
                .OrderBy(x => x.SortOrder)
                .ToList();

            foreach (var rootNode in rootNodes)
            {
                rootNode.GraphLevel = 0;
                rootNode.GraphColumn = 0;
            }

            var calculatingNodeIds = new HashSet<int>();

            int CalculateLevel(TopPartWorkflowNodeDto node)
                {
                    if (!calculatingNodeIds.Add(node.Id))
                    {
                        throw new InvalidOperationException(
                            $"Workflow graph contains a cycle at node {node.Id}.");
                    }

                    // PART drīkst būt bez parent connection.
                    if (node.NodeType == 1)
                        {
                            calculatingNodeIds.Remove(node.Id);
                            return 0;
                        }

                    var parentIds = connections
                        .Where(x => x.ToNodeId == node.Id)
                        .Select(x => x.FromNodeId)
                        .ToList();

                    if (parentIds.Count == 0)
                        {
                            calculatingNodeIds.Remove(node.Id);

                            if (node.NodeType == 4)
                                return 0;

                            throw new InvalidOperationException(
                                $"Workflow node {node.Id} has no parent connection.");
                        }

                    var parents = nodes
                        .Where(x => parentIds.Contains(x.Id))
                        .ToList();

                    if (parents.Count == 0)
                        {
                            calculatingNodeIds.Remove(node.Id);
                            return 0;
                        }

                    var level = parents.Max(CalculateLevel) + 1;

                    calculatingNodeIds.Remove(node.Id);

                    return level;
                }

            foreach (var node in nodes.Where(x => x.NodeType != 1))
                {
                    node.GraphLevel = CalculateLevel(node);
                }

            var nextColumn = 0;

            var root = nodes
                .Where(x => x.NodeType == 1)
                .OrderBy(x => x.SortOrder)
                .FirstOrDefault();

            if (root is not null)
            {
                void AssignBranchColumns(int parentNodeId)
                {
                    var childProcesses = connections
                        .Where(x => x.FromNodeId == parentNodeId)
                        .Join(
                            nodes.Where(x => x.NodeType == 2),
                            connection => connection.ToNodeId,
                            node => node.Id,
                            (connection, node) => node)
                        .OrderBy(x => x.SortOrder)
                        .ToList();

                    foreach (var processNode in childProcesses)
                        {
                            var inputWipColumns = connections
                                .Where(x => x.ToNodeId == processNode.Id)
                                .Join(
                                    nodes.Where(x => x.NodeType == 3),
                                    connection => connection.FromNodeId,
                                    node => node.Id,
                                    (connection, node) => node.GraphColumn)
                                .ToList();

                            var column = inputWipColumns.Count > 0
                                ? inputWipColumns.Average()
                                : nextColumn++;

                        processNode.GraphColumn = column;

                        if (!processNode.OutputWipNodeId.HasValue)
                            continue;

                        var outputWip = nodes.FirstOrDefault(x =>
                            x.Id == processNode.OutputWipNodeId.Value &&
                            x.NodeType == 3);

                        if (outputWip is null)
                            continue;

                        outputWip.GraphColumn = column;

                        AssignBranchColumns(outputWip.Id);
                    }
                }

                AssignBranchColumns(root.Id);
            }
            
        }
    }
}