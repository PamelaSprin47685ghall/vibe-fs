namespace Wanxiangshu.Domain

open System
open System.Collections.Generic
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// AGENT-028: Role × initial selected tier → SessionPersona (resolve-once at create).
/// Bookkeeper is InternalLeaf — not a public Role; use `bookkeeperPersona`.
[<RequireQualifiedAccess>]
module PersonaCatalog =

    let persona (role: Role) (tier: AgentTier) : string =
        match role, tier with
        | Role.Orchestrator, AgentTier.Fast -> "Integrator"
        | Role.Orchestrator, AgentTier.Deep -> "Director"
        | Role.Manager, AgentTier.Fast -> "Coordinator"
        | Role.Manager, AgentTier.Deep -> "Lead"
        | Role.Coder, AgentTier.Fast -> "Coder"
        | Role.Coder, AgentTier.Deep -> "Engineer"
        | Role.Inspector, AgentTier.Fast -> "Scout"
        | Role.Inspector, AgentTier.Deep -> "Investigator"
        | Role.DevOps, AgentTier.Fast -> "Technician"
        | Role.DevOps, AgentTier.Deep -> "Operator"
        | Role.Browser, AgentTier.Fast -> "Navigator"
        | Role.Browser, AgentTier.Deep -> "Researcher"
        | Role.Inquiry, AgentTier.Fast -> "Analyst"
        | Role.Inquiry, AgentTier.Deep -> "Inquirer"
        | Role.Reviewer, AgentTier.Fast -> "Examiner"
        | Role.Reviewer, AgentTier.Deep -> "Auditor"
        | Role.Blogger, AgentTier.Fast -> "Scribe"
        | Role.Blogger, AgentTier.Deep -> "Chronicler"
        | Role.Distiller, AgentTier.Fast -> "Condenser"
        | Role.Distiller, AgentTier.Deep -> "Distiller"

    let bookkeeperPersona (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "Clerk"
        | AgentTier.Deep -> "Curator"

    /// HOST-026 analogue: child / attached / InternalLeaf persona = owner persona.
    let inheritFrom (ownerPersona: string) : string = ownerPersona

/// AGENT-028 / PROMPT-014: session-create bind-once (process-local Phase 16).
/// Durable journal fact lands with Phase 17 resource parity.
[<RequireQualifiedAccess>]
module SessionPersona =

    let private gate = obj ()
    let private bySession = Dictionary<string, string>()

    let clearAllForTests () = lock gate (fun () -> bySession.Clear())

    let tryGet (sessionId: SessionId) : string option =
        lock gate (fun () ->
            match bySession.TryGetValue(SessionId.value sessionId) with
            | true, persona -> Some persona
            | false, _ -> None)

    let drop (sessionId: SessionId) : unit =
        lock gate (fun () -> bySession.Remove(SessionId.value sessionId) |> ignore)

    let bindOnce (sessionId: SessionId) (persona: string) : Result<string, string> =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match bySession.TryGetValue key with
            | true, existing when existing = persona -> Ok existing
            | true, existing ->
                Error(sprintf "SessionPersona already bound to %s; refusing %s (AGENT-028)" existing persona)
            | false, _ ->
                bySession.[key] <- persona
                Ok persona)

    /// AGENT-029 / STRENGTH-004: StrengthReplica inherits owner persona; no tier re-resolve.
    let inheritFromOwner (ownerPersona: string) (childId: SessionId) =
        bindOnce childId (PersonaCatalog.inheritFrom ownerPersona)
