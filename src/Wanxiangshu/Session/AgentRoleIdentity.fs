namespace Wanxiangshu.Session

open System
open Wanxiangshu.Kernel

/// SSOT: `Role` lives in `Kernel/Roles.fs`. This module only provides
/// Host-wire parsing and the canonical durable role label.
module AgentRoleIdentity =

    let ofRole (role: Role) : Role = role

    let ofManaged (agent: Wanxiangshu.OpenCode.ManagedAgent) : Role = agent.Role

    let toRole (role: Role) : Role = role

    let roleOfString (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Wanxiangshu.OpenCode.ManagedAgent.tryParse value |> Option.map ofManaged

    /// The canonical role label persisted in durable facts.
    ///
    /// Delegates to `ManagedAgentCatalog.roleLabel` rather than lowercasing
    /// `ToString()`. A DU-name spelling is a compiler artefact: renaming a case
    /// would silently change the durable string, and every `roleOfString` read of
    /// an older journal would then answer `None`.
    let roleName (role: Role) : string =
        Wanxiangshu.Domain.ManagedAgentCatalog.roleLabel role
