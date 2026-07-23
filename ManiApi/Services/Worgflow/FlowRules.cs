using ManiApi.DTOs.WorkFlow;

namespace ManiApi.Services.Workflow
{
    public class FlowRules
    {
        
        private readonly WorkflowFlowAnalyzer _analyzer;
        
        public FlowRules(WorkflowFlowAnalyzer analyzer)
        {
            _analyzer = analyzer;
            
        }

        internal bool HasOwnerProduct(FlowInfoDto flow)
        {
            return flow.OwnerProductToPartId != null;
        }

        internal bool IsFinishedFlow(FlowInfoDto flow)
        {
            return flow.FinishNode != null &&
                flow.IsFinished;
        }

        internal bool IsConsumedFlow(FlowInfoDto flow)
        {
            return flow.IsConsumed;
        }

        internal bool IsMergeCandidate(FlowInfoDto flow)
        {
            return IsFinishedFlow(flow) &&
                !IsConsumedFlow(flow);
        }

    }
}