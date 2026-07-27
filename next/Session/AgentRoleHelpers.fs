namespace Wanxiangshu.Next.Session

open System

module AgentRoleHelpers =

    let roleOfString (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            match value.Trim().ToLowerInvariant() with
            | "manager" -> Some AgentRole.Manager
            | "orchestrator" -> Some AgentRole.Orchestrator
            | "coder" -> Some AgentRole.Coder
            | "inspector" -> Some AgentRole.Inspector
            | "browser" -> Some AgentRole.Browser
            | "meditator" -> Some AgentRole.Meditator
            | "reviewer" -> Some AgentRole.Reviewer
            | "executor" -> Some AgentRole.Executor
            | _ -> None
