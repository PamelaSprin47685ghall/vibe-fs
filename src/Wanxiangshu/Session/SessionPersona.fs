namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// AGENT-028 / PROMPT-014: session-create bind-once (process-local Phase 16).
/// Durable journal fact lands with Phase 17 resource parity.
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
