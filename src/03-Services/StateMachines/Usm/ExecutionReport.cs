namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public enum ElementType { Node, Work, Transitioner }
    public enum FrameType { Start, Update, Stop }
    public enum ExecutionPhase { Entered, Exited }

    public readonly struct ExecutionReport
    {
        public ElementType ElementType { get; }
        public FrameType FrameType { get; }
        public ExecutionPhase Phase { get; }
        public OperationInfo OperationInfo { get; }

        public ExecutionReport(
            ElementType elementType,
            FrameType frameType,
            ExecutionPhase phase,
            OperationInfo result = default)
        {
            ElementType = elementType;
            FrameType = frameType;
            Phase = phase;
            OperationInfo = result;
        }
    }
}
