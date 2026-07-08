module Wanxiangshu.Kernel.ToolContext

open Wanxiangshu.Kernel.Domain

type ToolExecutionContext =
    { Directory: string
      SessionId: SessionId
      WorkspaceId: WorkspaceId option
      ChildRegistry: obj } // store dynamic ChildAgentRegistry so Shell stays detached but can access it
