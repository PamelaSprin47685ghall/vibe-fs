module Wanxiangshu.Kernel.ToolContext

open Wanxiangshu.Kernel.Domain

type ToolExecutionContext =
    { Directory: string
      SessionId: SessionId
      WorkspaceId: WorkspaceId option }
