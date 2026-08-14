namespace Wanxiangshu.Participant.Provider
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Collections.Generic
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity

/// HOST-026 / PROMPT-017: session-create bind-once (process-local Phase 2).
/// Durable journal fact lands with Phase 17 resource parity.
[<RequireQualifiedAccess>]
module SessionProviderLanguage =

    let private gate = obj ()
    let private bySession = Dictionary<string, ProviderLanguage>()

    let clearAllForTests () = lock gate (fun () -> bySession.Clear())

    let tryGet (sessionId: SessionId) : ProviderLanguage option =
        lock gate (fun () ->
            match bySession.TryGetValue(SessionId.value sessionId) with
            | true, lang -> Some lang
            | false, _ -> None)

    let drop (sessionId: SessionId) : unit =
        lock gate (fun () -> bySession.Remove(SessionId.value sessionId) |> ignore)

    /// Bind-once. Same language → Ok; conflict → Error; unbound → bind.
    let bindOnce (sessionId: SessionId) (language: ProviderLanguage) : Result<ProviderLanguage, string> =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            match bySession.TryGetValue key with
            | true, existing when existing = language -> Ok existing
            | true, existing ->
                Error(
                    sprintf
                        "SessionProviderLanguage already bound to %s; refusing %s (HOST-026)"
                        (ProviderLanguage.label existing)
                        (ProviderLanguage.label language)
                )
            | false, _ ->
                bySession.[key] <- language
                Ok language)

    /// HOST-026: child inherits owner language (no second global read).
    let inheritFromOwner (ownerLanguage: ProviderLanguage) (childId: SessionId) =
        bindOnce childId (ProviderLanguage.inheritFrom ownerLanguage)
