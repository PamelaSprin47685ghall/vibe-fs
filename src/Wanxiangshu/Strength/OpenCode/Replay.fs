namespace Wanxiangshu.Strength.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
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
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity
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

/// STRENGTH-008: Host-boundary Strength replay into the provider-facing
/// transcript, and Promoted→Traced close after XTrace capture.
/// Lifecycle math stays in StrengthLifecycle / StrengthTraceRecovery;
/// this module only wires Host messages, projection render, and durability.
[<RequireQualifiedAccess>]
module StrengthReplay =

    let private coveredThroughSequenceOf (journal: AgentJournal option) (owner: SessionId) =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind owner (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun state -> state.Blog)
            |> Option.map (fun blog ->
                blog.Coverage.IngestedThroughSequence
                |> XTraceCursor.create
                |> RecordCoverage.create
                |> RecordCoverage.ingestedThrough
                |> XTraceCursor.sequence))

    let private applyRenderedPlans
        (sessionId: string)
        (outObj: obj)
        (rawMessages: obj list)
        (plans: StrengthReplayPlan list)
        : Result<StrengthReplayPlan list, string> =
        result {
            if List.isEmpty plans then
                return []
            else
                let wire = ProviderWireCapture.decodeMessageView rawMessages

                let! intents =
                    StrengthLifecycle.replayIntents HostDigest.sha256Hex plans
                    |> Result.mapError (fun error -> sprintf "Strength replay intent refused: %A" error)

                let snapshot = { CurrentProjection = ProviderProjection.toSemantic wire }

                let rendered =
                    ProjectionRenderer.renderMessagesWithHostIds snapshot wire.Messages intents

                let! replayed =
                    ProjectionMessageEdit.tryApplyRenderedInsertionsPreservingBase
                        sessionId
                        HostDigest.sha256Hex
                        rawMessages
                        rendered
                    |> Result.mapError (fun error -> "Strength replay render failed: " + error)

                HostMessageProjection.replaceMessagesInPlace outObj replayed
                return plans
        }

    let private replayWithDurability
        (journal: AgentJournal option)
        (durability: StrengthDurabilityPort)
        (sessionId: string)
        (outObj: obj)
        : Task<Result<StrengthReplayPlan list, string>> =
        taskResult {
            let owner = SessionId.create sessionId
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput outObj
            let coveredThroughSequence = coveredThroughSequenceOf journal owner

            let! strengthProjection =
                durability.LoadProjection()
                |> TaskValue.map (Result.mapError (fun error -> "Strength replay projection failed: " + error))

            let! plans =
                StrengthLifecycle.replayPlans
                    owner
                    ProviderWireDecode.hostMessageId
                    rawMessages
                    durability.LoadFrameBundle
                    strengthProjection

            let plans =
                plans |> List.filter (StrengthLifecycle.needsRawReplay coveredThroughSequence)

            return! applyRenderedPlans sessionId outObj rawMessages plans
        }

    let private plansOrFailClosed
        (failClosed: string -> StrengthReplayPlan list)
        (work: Task<Result<StrengthReplayPlan list, string>>)
        : Task<StrengthReplayPlan list> =
        task {
            match! work with
            | Ok plans -> return plans
            | Error error -> return failClosed error
        }

    let private applyForSession
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailFuse: string -> unit)
        (sessionId: string)
        (outObj: obj)
        : Task<StrengthReplayPlan list> =
        let failClosed reason =
            strengthFailFuse reason
            raise (InvalidOperationException reason)

        match strengthDurability with
        | None -> Task.FromResult([])
        | Some durability -> plansOrFailClosed failClosed (replayWithDurability journal durability sessionId outObj)

    /// Replay durable Promoted frames before XTrace. Returns plans that still
    /// need Promoted→Traced close after capture (raw-replayed only).
    let applyBeforeXTrace
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailFuse: string -> unit)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : Task<StrengthReplayPlan list> =
        task {
            match projectionSessionIdOpt with
            | Some sessionId -> return! applyForSession journal strengthDurability strengthFailFuse sessionId outObj
            | None -> return []
        }

    let private resolveObservedParts
        (durable: AgentJournal)
        (parts: XTraceSemanticPartView list)
        : Task<Result<StrengthTraceObservedPart list, string>> =
        let rec loop (remaining: XTraceSemanticPartView list) (acc: StrengthTraceObservedPart list) =
            taskResult {
                match remaining with
                | [] -> return List.rev acc
                | part :: tail ->
                    let! body = durable.Writer.BlobWriter.Read part.TextRef

                    return!
                        loop
                            tail
                            ({ CursorSequence = XTraceCursor.sequence part.Cursor
                               Kind = part.Kind
                               ToolName = part.ToolName
                               Body = body }
                             :: acc)
            }

        loop parts []

    let private expectedHostIds (plan: StrengthReplayPlan) =
        plan.Bundle.Batches
        |> List.collect (fun batch ->
            [ StrengthFrame.hostMessageId
                  HostDigest.sha256Hex
                  plan.Prepared.OwnerSessionId
                  plan.Prepared.DecisionId
                  batch.RequestOrdinal
                  "call"
                  plan.Bundle.Digest
              StrengthFrame.hostMessageId
                  HostDigest.sha256Hex
                  plan.Prepared.OwnerSessionId
                  plan.Prepared.DecisionId
                  batch.RequestOrdinal
                  "result"
                  plan.Bundle.Digest ])

    let private tryStableTraceRange
        (plan: StrengthReplayPlan)
        (traceState: XTraceProjectionState)
        : StrengthTraceRange option =
        XTraceProjection.tryContiguousHostRange (expectedHostIds plan |> Set.ofList) traceState
        |> Option.map (fun range ->
            ({ StartInclusive = range |> XTraceRange.startInclusive |> XTraceCursor.sequence
               EndExclusive = range |> XTraceRange.endExclusive |> XTraceCursor.sequence }
            : StrengthTraceRange))

    let private recoverPlanTraceRange
        (durable: AgentJournal)
        (plan: StrengthReplayPlan)
        (traceState: XTraceProjectionState)
        : Task<Result<StrengthTraceRange option, string>> =
        taskResult {
            match tryStableTraceRange plan traceState with
            | Some value -> return Some value
            | None ->
                let! observed = resolveObservedParts durable (XTraceProjection.orderedSemanticParts traceState)
                return! StrengthTraceRecovery.recoverRange plan.Bundle observed
        }

    let private appendTracedOrFailClosed
        (durability: StrengthDurabilityPort)
        (failClosed: string -> unit)
        (plan: StrengthReplayPlan)
        (traced: StrengthTraceRange)
        : Task =
        task {
            match!
                durability.Append(
                    StrengthEvents.traced plan.Prepared.DecisionId traced.StartInclusive traced.EndExclusive
                )
            with
            | StrengthDurableAppend.Applied
            | StrengthDurableAppend.SemanticRejected _ -> ()
            | StrengthDurableAppend.StorageFailed error ->
                failClosed ("Strength Traced commit storage failure: " + error)
        }

    let private commitPlanTrace
        (durable: AgentJournal)
        (durability: StrengthDurabilityPort)
        (failClosed: string -> unit)
        (traceState: XTraceProjectionState)
        (plan: StrengthReplayPlan)
        : Task =
        task {
            match! recoverPlanTraceRange durable plan traceState with
            | Error error -> failClosed ("Strength Traced recovery failed: " + error)
            | Ok None -> failClosed "Strength Promoted frame is absent from XTrace after replay capture"
            | Ok(Some traced) -> do! appendTracedOrFailClosed durability failClosed plan traced
        }

    let private commitCapturedPlans
        (durable: AgentJournal)
        (durability: StrengthDurabilityPort)
        (failClosed: string -> unit)
        (updated: XTraceProjectionState)
        (plans: StrengthReplayPlan list)
        : Task =
        task {
            let pending = plans |> List.filter (fun plan -> plan.ExistingTraceRange.IsNone)

            for plan in pending do
                do! commitPlanTrace durable durability failClosed updated plan
        }

    /// Close Promoted → Traced after XTrace capture for plans that lacked a
    /// prior trace range. Stable Host ids recover the exact range; legacy
    /// positional traces fall back to unique canonical match (fail closed).
    let commitTracedAfterCapture
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailClosed: string -> unit)
        (traceState: XTraceProjectionState option)
        (plans: StrengthReplayPlan list)
        : Task =
        task {
            match journal, strengthDurability, traceState with
            | Some durable, Some durability, Some updated ->
                do! commitCapturedPlans durable durability strengthFailClosed updated plans
            | _ -> ()
        }
