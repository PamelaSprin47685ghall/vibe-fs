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

    let toRole (role: AgentRole) : Role =
        match role with
        | AgentRole.Manager -> Role.Manager
        | AgentRole.Orchestrator -> Role.Orchestrator
        | AgentRole.Coder -> Role.Coder
        | AgentRole.Inspector -> Role.Inspector
        | AgentRole.Browser -> Role.Browser
        | AgentRole.Meditator -> Role.Meditator
        | AgentRole.Reviewer -> Role.Reviewer
        | AgentRole.DevOps -> Role.DevOps
        | AgentRole.Executor -> Role.Executor
        | AgentRole.Blogger -> Role.Blogger

    let defaultFastManagedName (role: AgentRole) : string =
        ManagedAgent.nameOf AgentTier.Fast (toRole role)

    let roleOfString (value: string) =
        if String.IsNullOrWhiteSpace value then None
        else ManagedAgent.tryParse value |> Option.map ofManaged
