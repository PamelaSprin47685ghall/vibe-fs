namespace Wanxiangshu.Session

open System
open Wanxiangshu.Kernel
open Wanxiangshu.OpenCode

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

    /// `AgentRole` and `Role` are the same ten cases spelled twice.
    ///
    /// SSOT has one Role (AGENT-001), so this converter exists only while
    /// `AgentRole` still threads through ForkRuntime, ChildRun and PtyHandle.
    /// Deleting the duplicate type belongs to packages E/F; until then the
    /// conversion is explicit rather than implicit, so every crossing is visible.
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

    let roleOfString (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            ManagedAgent.tryParse value |> Option.map ofManaged

    /// The canonical role label persisted in durable facts.
    ///
    /// Delegates to `ManagedAgentCatalog.roleLabel` rather than lowercasing
    /// `ToString()`. A DU-name spelling is a compiler artefact: renaming a case
    /// would silently change the durable string, and every `roleOfString` read of
    /// an older journal would then answer `None`.
    let roleName (role: AgentRole) : string =
        Wanxiangshu.Domain.ManagedAgentCatalog.roleLabel (toRole role)
