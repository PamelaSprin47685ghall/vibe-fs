// primary_owner: host-boundary — OpenCode.PluginTransforms (plugin-transforms-composition) — COMPOSITION-ROOT — 13-step static score, no dynamic pipeline
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
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.OpenCode.Host.RequirementGrounding
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
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
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

    type NormalTransformCapabilities =
        { BeginPhysicalProviderAttempt: string option -> obj -> Task<unit>
          BindSessionStartedAt: string option -> Task<DateTimeOffset option>
          ApplyStrengthReplay: string option -> obj -> Task<StrengthReplayPlan list>
          ApplyXTracePipeline: string option -> obj -> StrengthReplayPlan list -> Task<unit>
          ApplyCompanion: string option -> obj -> obj -> Task<unit>
          ApplyXWire: obj -> Task<PrefixPresentationHorizon>
          ApplyEnforcerContinuation: string option -> obj -> Task<unit>
          ApplyStrengthSpeculate: obj -> Task<unit>
          InjectPairGuideline: string option -> DateTimeOffset option -> obj -> Task<unit>
          ProjectRequirementGrounding: string option -> obj -> Task<unit>
          InjectBloggerChronicle: string option -> obj -> unit
          SanitizeMessages: obj -> unit
          InterruptAfterSubmittedJudgement: string option -> Task<unit> }

    type TransformBranchCapabilities =
        { IsExplicitResume: string option -> obj -> bool
          RegisterOwned: string -> unit
          ReplicaRuntime: string option -> StrengthReplicaRuntime option
          ReplicaXWire: obj -> Task<unit>
          ReplicaSanitize: obj -> unit
          ExplicitResumeSanitize: obj -> unit }


    let private languageFor (projectionSessionIdOpt: string option) : ProviderLanguage =
        match projectionSessionIdOpt with
        | Some sessionId -> ProviderLanguageBinding.ensureRoot (SessionId.create sessionId)
        | None -> ProviderLanguage.English

    // Explicit composition mode — replaces the previous implicit helper dispatch
    // (strengthReplicaRuntime / isExplicitResumeProviderMaterial / ordinaryProviderTransform).
    // This type is representation-level (composition topology), not a foreign domain decision.
    type private TransformMode =
        | ExplicitResumeDisclosure
        | StrengthReplica of StrengthReplicaRuntime
        | Ordinary

    let private failIfReplicaDecisionLost (handled: bool) : unit =
        if not handled then
            raise (InvalidOperationException "StrengthReplica transform lost its live decision binding")

    let private determineTransformMode
        (branches: TransformBranchCapabilities)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : TransformMode =
        match
            branches.IsExplicitResume projectionSessionIdOpt outObj, branches.ReplicaRuntime projectionSessionIdOpt
        with
        | true, _ -> ExplicitResumeDisclosure
        | false, Some runtime -> StrengthReplica runtime
        | false, None -> Ordinary

    let defaultCapabilities (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : NormalTransformCapabilities =
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
                match wired.CurrentPhysicalUserMessage(SessionId.value sessionId) with
                | None -> Task.FromResult(Error "MANAGED-SESSION-017: current authority root unavailable")
                | Some physical ->
                    ManagedSessionTermination.terminate
                        (fun ownerId -> scope.CancelSessionChildren(SessionId.value ownerId))
                        sessionPort
                        eventPort
                        sessionId
                        (physical
                         |> PhysicalUserMessageId.create
                         |> PhysicalUserMessageId.promoteToAuthorityRoot)
                        reason

        { BeginPhysicalProviderAttempt =
            SessionExecutionBinding.beginPhysicalProviderAttemptForTransform
                scope.Sessions.Quiescence.BeginProviderAttempt
          BindSessionStartedAt =
            SessionStartedAtLedger.bindSessionStartedAt journal clock terminateSession Diagnostic.emit
          ApplyStrengthReplay = StrengthReplay.applyBeforeXTrace journal strengthDurability strengthFailFuse
          ApplyXTracePipeline =
            fun projectionSessionIdOpt outObj strengthReplayPlans ->
                task {
                    do!
                        XTracePipeline.applyPipeline
                            journal
                            strengthDurability
                            strengthFailFuse
                            scope.Sessions.Companions
                            projectionSessionIdOpt
                            outObj
                            strengthReplayPlans
                }
          ApplyCompanion =
            CompanionTransform.applyCompanionForOrdinaryMaterial
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
                (fun projectionSessionIdOpt outObj ->
                    ExplicitResumeSuppression.isCurrentMaterial outObj
                    || ExplicitResumeSuppression.isExplicitResumeBinding projectionSessionIdOpt outObj)
          ApplyXWire = XWire.applyTransform snapshotOpt journal scope
          ApplyEnforcerContinuation =
            fun projectionSessionIdOpt outObj ->
                task {
                    do!
                        EnforcerContinuation.applyContinuation
                            scope
                            journal
                            terminateSession
                            projectionSessionIdOpt
                            outObj
                }
          ApplyStrengthSpeculate = StrengthSpeculate.tryApply snapshotOpt journal strengthDurability scope
          InjectPairGuideline =
            fun projectionSessionIdOpt sessionStartedAt outObj ->
                task {
                    do!
                        PairProgrammingThoughtTransform.maybeInjectGuideline
                            journal
                            projectionSessionIdOpt
                            sessionStartedAt
                            clock
                            terminateSession
                            (languageFor projectionSessionIdOpt)
                            outObj
                }
          ProjectRequirementGrounding =
            RequirementGroundingTransform.projectOrTerminate journal workspaceDirectory terminateSession
          InjectBloggerChronicle =
            fun projectionSessionIdOpt outObj ->
                BloggerChronicleText.maybeInject
                    journal
                    projectionSessionIdOpt
                    (languageFor projectionSessionIdOpt)
                    outObj
          SanitizeMessages = HostMessageProjection.sanitizeOutputMessages
          InterruptAfterSubmittedJudgement =
            JudgeTool.interruptAfterSubmittedJudgement
                journal
                scope.BloggerRuntimeHost.Cancellation
                wired.CurrentPhysicalUserMessage
                scope.RunBackground
                sessionPort }

    let defaultBranchCapabilities (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : TransformBranchCapabilities =
        let scope = boot.Scope
        let journal = boot.Journal
        let snapshotOpt = host.SnapshotOpt
        let wired = host.Wired

        { IsExplicitResume =
            fun projectionSessionIdOpt outObj ->
                ExplicitResumeSuppression.isCurrentMaterial outObj
                || ExplicitResumeSuppression.isExplicitResumeBinding projectionSessionIdOpt outObj
          RegisterOwned = wired.RegisterOwned
          ReplicaRuntime =
            fun projectionSessionIdOpt ->
                match projectionSessionIdOpt, scope.Strength.StrengthReplicaRuntime with
                | Some sessionId, Some runtime when runtime.IsReplica(SessionId.create sessionId) -> Some runtime
                | _ -> None
          ReplicaXWire =
            fun outObj ->
                task {
                    let! _ = XWire.applyTransform snapshotOpt journal scope outObj
                    return ()
                }
          ReplicaSanitize = HostMessageProjection.sanitizeOutputMessages
          ExplicitResumeSanitize = HostMessageProjection.sanitizeOutputMessages }

    let normalTransform
        (caps: NormalTransformCapabilities)
        (projectionSessionIdOpt: string option)
        (inObj: obj)
        (outObj: obj)
        : Task<unit> =
        task {
            // 1. SessionExecutionBinding.beginPhysicalProviderAttemptForTransform
            do! caps.BeginPhysicalProviderAttempt projectionSessionIdOpt outObj

            // 2. SessionStartedAtLedger.tryBindOrAbort
            let! sessionStartedAt = caps.BindSessionStartedAt projectionSessionIdOpt

            // 3. StrengthReplay.applyBeforeXTrace
            let! strengthReplayPlans = caps.ApplyStrengthReplay projectionSessionIdOpt outObj

            // 4. XTracePipeline.applyPipeline
            do! caps.ApplyXTracePipeline projectionSessionIdOpt outObj strengthReplayPlans

            // 5. applyCompanionForOrdinaryMaterial
            do! caps.ApplyCompanion projectionSessionIdOpt inObj outObj

            // 6. XWire.applyTransform. A selected prefix probe creates a
            // tentative cold horizon for this physical request; downstream
            // historical auxiliaries must not replay the old horizon into it.
            let! prefixHorizon = caps.ApplyXWire outObj

            // 7. EnforcerContinuation.applyContinuation
            do! caps.ApplyEnforcerContinuation projectionSessionIdOpt outObj

            if prefixHorizon = PrefixPresentationHorizon.Current then
                // 8. StrengthSpeculate.tryApply
                do! caps.ApplyStrengthSpeculate outObj

                // 9. PairProgrammingThoughtTransform.maybeInjectGuideline
                do! caps.InjectPairGuideline projectionSessionIdOpt sessionStartedAt outObj

                // 10. RequirementGroundingTransform.projectOrTerminate
                do! caps.ProjectRequirementGrounding projectionSessionIdOpt outObj

            // 11. BloggerChronicleText.maybeInject
            caps.InjectBloggerChronicle projectionSessionIdOpt outObj

            // 12. HostMessageProjection.sanitizeMessages
            caps.SanitizeMessages outObj

            // 13. JudgeTool.interruptAfterSubmittedJudgement
            do! caps.InterruptAfterSubmittedJudgement projectionSessionIdOpt
            ()
        }

    let createWithCaps
        (caps: NormalTransformCapabilities)
        (branches: TransformBranchCapabilities)
        : obj -> obj -> Task<unit> =
        fun (inObj: obj) (outObj: obj) ->
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

                match determineTransformMode branches projectionSessionIdOpt outObj with
                | ExplicitResumeDisclosure ->
                    // CRASH-018: the exact /continue material stays disclosure-only
                    // for every provider step, including steps after tool results.
                    // The trailing marker is the direct path; the exact physical
                    // registry is the authoritative fallback when Host projection
                    // drops custom part metadata after chat.message.
                    // Do not reinterpret it through ordinary semantic transforms.
                    branches.ExplicitResumeSanitize outObj
                | StrengthReplica runtime ->
                    projectionSessionIdOpt |> Option.iter branches.RegisterOwned
                    // STRENGTH-004/009: Replica uses exactly one request-plan
                    // writer plus its mirror/K gate. XTrace, Manager narrative,
                    // Companion, Enforcer, Pair and Review are owner-only.
                    do! branches.ReplicaXWire outObj
                    let! handled = runtime.HandleTransform outObj
                    do failIfReplicaDecisionLost handled
                    branches.ReplicaSanitize outObj
                | Ordinary ->
                    projectionSessionIdOpt |> Option.iter branches.RegisterOwned
                    do! normalTransform caps projectionSessionIdOpt inObj outObj
            }

    /// Provider-facing transform composition: order only.
    /// Strength replay/trace → StrengthReplay; speculation → StrengthSpeculate;
    /// narrative → ManagerNarrativeTransform; seal → ReviewSeal; replica fast path unchanged.
    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : obj -> obj -> Task<unit> =
        let caps = defaultCapabilities boot host
        let branches = defaultBranchCapabilities boot host
        createWithCaps caps branches
