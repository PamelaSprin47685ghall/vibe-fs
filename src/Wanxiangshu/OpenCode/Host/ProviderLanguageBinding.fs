namespace Wanxiangshu.OpenCode

open System
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
open Wanxiangshu.Participant.Provider
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
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Foundation.Identity

/// HOST-026: global preference → root bind; child inherits owner.
/// Preference source: `WANXIANGSHU_PROVIDER_LANGUAGE` (`en` | `zh-CN`). Default `en`.
[<RequireQualifiedAccess>]
module ProviderLanguageBinding =

    let readGlobalPreference () : ProviderLanguage =
        match
            Environment.GetEnvironmentVariable "WANXIANGSHU_PROVIDER_LANGUAGE"
            |> Option.ofObj
        with
        | None -> ProviderLanguage.English
        | Some raw when String.IsNullOrWhiteSpace raw -> ProviderLanguage.English
        | Some raw ->
            match ProviderLanguage.tryParse raw with
            | Some lang -> lang
            | None ->
                raise (
                    InvalidOperationException(sprintf "WANXIANGSHU_PROVIDER_LANGUAGE unrecognized: %s (HOST-026)" raw)
                )

    /// Root / first-touch: bind from global preference once.
    let ensureRoot (sessionId: SessionId) : ProviderLanguage =
        let bindFromGlobal () =
            match SessionProviderLanguage.bindOnce sessionId (readGlobalPreference ()) with
            | Ok lang -> lang
            | Error msg -> raise (InvalidOperationException msg)

        match SessionProviderLanguage.tryGet sessionId with
        | Some lang -> lang
        | None -> bindFromGlobal ()

    /// Child / attached / InternalLeaf: inherit owner|commissioner; never re-read global.
    let ensureInherited (ownerId: SessionId) (childId: SessionId) : ProviderLanguage =
        let ownerLang = ensureRoot ownerId

        match SessionProviderLanguage.inheritFromOwner ownerLang childId with
        | Ok lang -> lang
        | Error msg -> raise (InvalidOperationException msg)
