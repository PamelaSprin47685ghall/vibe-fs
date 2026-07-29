namespace Wanxiangshu.Next.Session

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.OpenCode

module AgentRoleIdentity =

    let ofRole (role: Role) : AgentRole =
        match role with
        | Role.Manager -> AgentRole.Manager
        | Role.Orchestrator -> AgentRole.Orchestrator
        | Role.Coder -> AgentRole.Coder
        | Role.Inspector -> AgentRole.Inspector
        | Role.Browser -> AgentRole.Browser
        | Role.Meditator -> AgentRole.Meditator
        | Role.Reviewer -> AgentRole.Reviewer
        | Role.DevOps -> AgentRole.DevOps
        | Role.Executor -> AgentRole.Executor
        | Role.Blogger -> AgentRole.Blogger

    let ofManaged (agent: ManagedAgent) : AgentRole = ofRole agent.Role

    let roleOfString (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            ManagedAgent.tryParse value |> Option.map ofManaged
