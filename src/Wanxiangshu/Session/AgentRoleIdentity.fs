namespace Wanxiangshu.Session

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel

/// SSOT: `Role` lives in `Kernel/Roles.fs`. This module only provides
/// Host-wire parsing and the canonical durable role label.
module AgentRoleIdentity =

    let ofRole (role: Role) : Role = role

    /// Host composition passes `managed.Role`; Session never depends on OpenCode types.
    let ofManaged (role: Role) : Role = role

    let toRole (role: Role) : Role = role

    /// Host wire names (`fast-manager`) parse via Domain SSOT; bare role labels fall back to catalog.
    let roleOfString (value: string) : Role option =
        if String.IsNullOrWhiteSpace value then
            None
        else
            match PromptAuthority.parseAgentNameTyped value with
            | Ok parsed -> Some parsed.Role
            | Error _ -> ManagedAgentCatalog.tryParseRole (value.Trim().ToLowerInvariant())

    /// The canonical role label persisted in durable facts.
    ///
    /// Delegates to `ManagedAgentCatalog.roleLabel` rather than lowercasing
    /// `ToString()`. A DU-name spelling is a compiler artefact: renaming a case
    /// would silently change the durable string, and every `roleOfString` read of
    /// an older journal would then answer `None`.
    let roleName (role: Role) : string = ManagedAgentCatalog.roleLabel role
