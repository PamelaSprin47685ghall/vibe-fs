module Wanxiangshu.Kernel.ToolContext

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

type ToolExecutionContext =
    { Directory: string
      SessionId: SessionId
      WorkspaceId: WorkspaceId option
      ChildRegistry: obj } // store dynamic ChildAgentRegistry so Shell stays detached but can access it
