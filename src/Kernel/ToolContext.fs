module VibeFs.Kernel.ToolContext

type ToolExecutionContext = {
    Directory: string
    SessionId: string
    WorkspaceId: string option
}