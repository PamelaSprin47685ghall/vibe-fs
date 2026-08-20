namespace Wanxiangshu.Context.Trace

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

module XTracePipeline =

    let private raiseFailClosed (fuse: string -> unit) (reason: string) : 'a =
        fuse reason
        raise (InvalidOperationException reason)

    /// Strip AssistancePrompt sentinel from reasoning wire parts only.
    let private stripReasoningSentinel (part: ProviderWireCapture.CapturedWirePart) =
        match part.WirePart with
        | WireReasoning text ->
            { part with
                WirePart = WireReasoning(AssistancePrompt.stripSentinel text) }
        | WireText _
        | WireToolCall _
        | WireToolResult _
        | WireMedia _ -> part

    let private capturedMessagesStripped (rawMessages: obj list) =
        ProviderWireCapture.decodeCapturedMessageView rawMessages
        |> List.map (fun message ->
            { message with
                Parts = message.Parts |> List.map stripReasoningSentinel })

    /// Evidence -> Decision: all Host message ids present -> stable id list.
    let private tryStableHostMessageIds (rawMessages: obj list) : string list option =
        let ids = rawMessages |> List.map ProviderWireDecode.hostMessageId

        if ids |> List.forall Option.isSome then
            Some(ids |> List.map Option.get)
        else
            None

    let private captureStableOrFailClosed
        (journal: AgentJournal option)
        (sessionIdentity: SessionId)
        (ids: string list)
        (capturedMessages: ProviderWireCapture.CapturedWireMessage list)
        (strengthFailFuse: string -> unit)
        : Task<XTraceProjectionState option> =
        task {
            match! XTraceCapture.captureMessageViewStable journal sessionIdentity ids capturedMessages with
            | Ok state -> return state
            | Error error -> return raiseFailClosed strengthFailFuse error
        }

    let private captureTraceState
        (journal: AgentJournal option)
        (sessionIdentity: SessionId)
        (stableMessageIds: string list option)
        (capturedMessages: ProviderWireCapture.CapturedWireMessage list)
        (strengthFailFuse: string -> unit)
        : Task<XTraceProjectionState option> =
        match stableMessageIds with
        | Some ids when XTraceCapture.supportsStableInsertion journal sessionIdentity ->
            captureStableOrFailClosed journal sessionIdentity ids capturedMessages strengthFailFuse
        | _ -> XTraceCapture.captureMessageView journal sessionIdentity capturedMessages

    let private refreshCompanionXTrace
        (companions: Dictionary<string, CompanionHost>)
        (sessionId: string)
        (updated: XTraceProjectionState)
        =
        match companions.TryGetValue sessionId with
        | true, host -> host.RefreshXTrace updated
        | false, _ -> ()

    let private applyManagerNarrativeRewrite
        (journal: AgentJournal option)
        (sessionIdOpt: string option)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        (outObj: obj)
        : Task =
        task {
            match! ManagerNarrativeTransform.tryTransform journal sessionIdOpt traceState rawMessages with
            | Some rewritten -> HostMessageProjection.replaceMessagesInPlace outObj rewritten
            | None -> ()
        }

    let private applySessionXTraceAndNarrative
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailFuse: string -> unit)
        (companions: Dictionary<string, CompanionHost>)
        (sessionId: string)
        (outObj: obj)
        (strengthReplayPlans: StrengthReplayPlan list)
        : Task =
        task {
            let rawMessages = unbox<obj array> outObj?messages |> Array.toList
            let capturedMessages = capturedMessagesStripped rawMessages

            let _semantic =
                ProviderWireCapture.wireMessageView capturedMessages
                |> ProviderProjection.toSemantic

            let sessionIdentity = SessionId.create sessionId
            let stableMessageIds = tryStableHostMessageIds rawMessages

            let! traceState =
                captureTraceState journal sessionIdentity stableMessageIds capturedMessages strengthFailFuse

            do!
                StrengthReplay.commitTracedAfterCapture
                    journal
                    strengthDurability
                    (raiseFailClosed strengthFailFuse)
                    traceState
                    strengthReplayPlans

            traceState |> Option.iter (refreshCompanionXTrace companions sessionId)

            do! applyManagerNarrativeRewrite journal (Some sessionId) traceState rawMessages outObj
        }

    let applyPipeline
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailFuse: string -> unit)
        (companions: Dictionary<string, CompanionHost>)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        (strengthReplayPlans: StrengthReplayPlan list)
        : Task =
        match projectionSessionIdOpt with
        | Some sessionId ->
            applySessionXTraceAndNarrative
                journal
                strengthDurability
                strengthFailFuse
                companions
                sessionId
                outObj
                strengthReplayPlans
        | None -> Task.FromResult()
