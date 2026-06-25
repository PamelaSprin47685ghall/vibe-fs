module VibeFs.Kernel.ToolContext

open VibeFs.Kernel.Domain

type ToolExecutionContext = {
    Directory: string
    SessionId: SessionId
    WorkspaceId: WorkspaceId option
}
