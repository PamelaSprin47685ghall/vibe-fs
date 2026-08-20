namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
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
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open PluginHostInterop

module PluginTransforms =

    type private SessionTermination = SessionId -> string -> Task<Result<unit, string>>

    let private raiseStrengthFailClosed (fuse: string -> unit) (reason: string) : 'a =
        fuse reason
        raise (InvalidOperationException reason)

    let private languageFor (projectionSessionIdOpt: string option) : ProviderLanguage =
        match projectionSessionIdOpt with
        | Some sessionId -> ProviderLanguageBinding.ensureRoot (SessionId.create sessionId)
        | None -> ProviderLanguage.English

    let private strengthReplicaRuntime
        (projectionSessionIdOpt: string option)
        (scope: PluginRuntimeScope)
        : StrengthReplicaRuntime option =
        match projectionSessionIdOpt, scope.Strength.StrengthReplicaRuntime with
        | Some sessionId, Some runtime when runtime.IsReplica(SessionId.create sessionId) -> Some runtime
        | _ -> None

    let private requireReplicaHandled (handled: bool) =
        if not handled then
            raise (InvalidOperationException "StrengthReplica transform lost its live decision binding")

    let private isExplicitResumeProviderMaterial projectionSessionIdOpt outObj =
        ExplicitResumeSuppression.isCurrentMaterial outObj
        || ExplicitResumeSuppression.isExplicitResumeBinding projectionSessionIdOpt outObj

    /// Provider-facing transform composition: order only.
    /// Strength replay/trace → StrengthReplay; speculation → StrengthSpeculate;
    /// narrative → ManagerNarrativeTransform; seal → ReviewSeal; replica fast path unchanged.
    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : obj -> obj -> Task<unit> =
        let scope = boot.Scope
        let journal = boot.Journal
        let clock = boot.Clock
        let workspaceDirectory = boot.WorkspaceDirectory
        let sessionPort = host.SessionPort
        let eventPort = host.EventPort
        let snapshotOpt = host.SnapshotOpt
        let strengthDurability = host.StrengthDurability
        let wired = host.Wired
        let strengthFailFuse = boot.StrengthFailClosed

        let terminateSession: SessionTermination =
            fun sessionId reason ->
                ManagedSessionTermination.terminate
                    (fun ownerId -> scope.CancelSessionChildren(SessionId.value ownerId))
                    sessionPort
                    eventPort
                    sessionId
                    reason

        let applyCompanionForOrdinaryMaterial projectionSessionIdOpt inObj outObj =
            if ExplicitResumeSuppression.isCurrentMaterial outObj then
                AsyncSupport.completedTask ()
            else
                CompanionTransform.handleCompanionTransform
                    scope.Sessions.Companions
                    scope.Sessions.CompanionGate
                    scope
                    sessionPort
                    journal
                    (Some(fun bloggerId ->
                        // Register ownership + ActiveRun so idle→reconcile
                        // emits TerminalOutcome.Completed for this child.
                        wired.RegisterOwned(SessionId.value bloggerId)
                        wired.BindActiveRun bloggerId Role.Blogger None))
                    SharedState.RootWorkspace
                    inObj
                    outObj

        let normalTransform (projectionSessionIdOpt: string option) (inObj: obj) (outObj: obj) : Task<unit> =
            task {
                // HOST-004：新 provider request 开始构建 → 旧 idle permit
                // 立即失效。必须在该 transform 的最早同步位置（任何 let!
                // 之前）调用，不得等 request 已运行才标 Running。
                match projectionSessionIdOpt with
                | Some sessionId ->
                    do!
                        SessionExecutionBinding.beginPhysicalProviderAttemptForTransform
                            scope.Sessions.Quiescence.BeginProviderAttempt
                            (SessionId.create sessionId)
                            outObj
                | None -> ()

                // TIME-007: the first provider-facing prompt is the session's
                // creation boundary. Sample synchronously before any await, then
                // bind once durably; later prompts reuse the projection value.
                let sessionStartCandidate =
                    projectionSessionIdOpt |> Option.map (fun _ -> clock.UtcNow())

                let! sessionStartedAt =
                    task {
                        match!
                            SessionStartedAtLedger.tryBindOrAbort journal projectionSessionIdOpt sessionStartCandidate
                        with
                        | Ok startedAt -> return startedAt
                        | Error reason ->
                            match projectionSessionIdOpt with
                            | Some sessionId ->
                                Diagnostic.emit
                                    "host-013-session-start-bind-failed"
                                    [ "session_id", sessionId; "result", reason ]

                                let terminalReason = "HOST-013 SessionStartedAt bind failed: " + reason
                                let! _ = terminateSession (SessionId.create sessionId) terminalReason
                                return raise (InvalidOperationException terminalReason)
                            | None ->
                                Diagnostic.emit
                                    "host-013-session-start-bind-failed"
                                    [ "session_id", ""; "result", reason ]

                                return
                                    raise (
                                        InvalidOperationException("HOST-013 SessionStartedAt bind failed: " + reason)
                                    )
                    }

                let! strengthReplayPlans =
                    match projectionSessionIdOpt with
                    | Some sessionId ->
                        StrengthReplay.applyBeforeXTrace
                            journal
                            strengthDurability
                            (raiseStrengthFailClosed strengthFailFuse)
                            sessionId
                            outObj
                    | None -> Task.FromResult []

                // COMPANION-003/007: keep the XTrace in step with the
                // provider-visible semantic projection at the transform
                // boundary — BEFORE the Companion rewrite and X-wire run,
                // so the ingest cursor maps against the trace that now
                // exists (not the previous round's mirror) and the XTrace
                // never absorbs synthetic heads (Companion memory / prefix
                // replacement) as raw parts.
                // Idempotent by (turn, part) provenance; a lagging trace
                // would stall BlogObservationCommitted.
                do!
                    XTracePipeline.applyPipeline
                        journal
                        strengthDurability
                        strengthFailFuse
                        scope.Sessions.Companions
                        projectionSessionIdOpt
                        outObj
                        strengthReplayPlans

                do! applyCompanionForOrdinaryMaterial projectionSessionIdOpt inObj outObj

                do! XWire.applyTransform snapshotOpt journal scope outObj

                // docs/what/enforcer.md ENFORCER-044/047/050: Blogger continuation only.
                // Main-session material is decided once in
                // CompanionTransform → BloggerCoordinator.onMainMaterial.
                do! EnforcerContinuation.applyContinuation scope journal terminateSession projectionSessionIdOpt outObj

                // STRENGTH-009: freeze the post-Enforcer semantic view and
                // complete any eligible speculation before the Pair marker.
                // Prepared publication precedes Candidate visibility.
                do! StrengthSpeculate.tryApply snapshotOpt journal strengthDurability scope outObj

                // HOST-013：永久 pair-programming auto-injected。
                // XTrace 之后、ReviewSeal 之前。恢复 durable 历史 pair，
                // 再在 ResultGap 写入本次 completed synthetic skill({name:""}) Host 行。
                // Companion / Blogger 整段跳过：结对编程约束干扰 blog 工具合同。
                do!
                    PairProgrammingThoughtTransform.maybeInjectGuideline
                        journal
                        projectionSessionIdOpt
                        sessionStartedAt
                        clock
                        terminateSession
                        (languageFor projectionSessionIdOpt)
                        outObj

                // REQUIREMENT-GROUNDING-007/012: permanent requirement reads use
                // the same append-only placement discipline, after HOST-013 so
                // ordinary and Cursor order is always pseudo-skill → read(s).
                do!
                    RequirementGroundingTransform.projectOrTerminate
                        journal
                        workspaceDirectory
                        projectionSessionIdOpt
                        (fun sessionId reason -> terminateSession (SessionId.create sessionId) reason)
                        outObj

                BloggerChronicleText.maybeInject
                    journal
                    projectionSessionIdOpt
                    (languageFor projectionSessionIdOpt)
                    outObj

                // HOST-016: 对 provider-facing 消息做非空 content 兜底保障，
                // 避免仅推理/空 content 导致上游 API 报 400 messages[i].content cannot be empty。
                let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                HostMessageProjection.replaceMessagesInPlace outObj sanitized
                ()
            }

        let ordinaryProviderTransform projectionSessionIdOpt inObj outObj =
            task {
                projectionSessionIdOpt |> Option.iter wired.RegisterOwned

                match strengthReplicaRuntime projectionSessionIdOpt scope with
                | Some runtime ->
                    // STRENGTH-004/009: Replica uses exactly one request-plan
                    // writer plus its mirror/K gate. XTrace, Manager narrative,
                    // Companion, Enforcer, Pair and Review are owner-only.
                    do! XWire.applyTransform snapshotOpt journal scope outObj
                    let! handled = runtime.HandleTransform outObj
                    requireReplicaHandled handled

                    let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                    let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                    HostMessageProjection.replaceMessagesInPlace outObj sanitized
                | None -> do! normalTransform projectionSessionIdOpt inObj outObj
            }

        let transform (inObj: obj) (outObj: obj) : Task<unit> =
            task {
                let projectionSessionIdOpt =
                    projectionSessionIdFromMessages outObj
                    |> Option.orElseWith (fun () ->
                        if not (isNull inObj) && not (isNull inObj?sessionID) then
                            let sid = string inObj?sessionID
                            if String.IsNullOrWhiteSpace sid then None else Some sid
                        elif not (isNull inObj) && not (isNull inObj?sessionId) then
                            let sid = string inObj?sessionId
                            if String.IsNullOrWhiteSpace sid then None else Some sid
                        else
                            None)

                if isExplicitResumeProviderMaterial projectionSessionIdOpt outObj then
                    // CRASH-018: the exact /continue material stays disclosure-only
                    // for every provider step, including steps after tool results.
                    // The trailing marker is the direct path; the exact physical
                    // registry is the authoritative fallback when Host projection
                    // drops custom part metadata after chat.message.
                    // Do not reinterpret it through ordinary semantic transforms.
                    let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                    let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                    HostMessageProjection.replaceMessagesInPlace outObj sanitized
                else
                    do! ordinaryProviderTransform projectionSessionIdOpt inObj outObj
            }

        transform
