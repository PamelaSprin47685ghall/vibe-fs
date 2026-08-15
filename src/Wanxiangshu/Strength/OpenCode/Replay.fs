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

    /// Replay durable Promoted frames before XTrace. Returns plans that still
    /// need Promoted→Traced close after capture (raw-replayed only).
    let applyBeforeXTrace
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailClosed: string -> StrengthReplayPlan list)
        (sessionId: string)
        (outObj: obj)
        : Task<StrengthReplayPlan list> =
        task {
            match strengthDurability with
            | None -> return []
            | Some durability ->
                let owner = SessionId.create sessionId
                let rawMessages = ProviderWireDecode.messagesFromTransformOutput outObj

                let coveredThroughSequence =
                    journal
                    |> Option.bind (fun durable ->
                        AgentProjection.tryFind owner (AgentJournal.snapshot durable).AgentProjections
                        |> Option.bind (fun state -> state.Blog)
                        |> Option.map (fun blog -> blog.Coverage.IngestedThroughSequence))

                match! durability.LoadProjection() with
                | Error error -> return strengthFailClosed ("Strength replay projection failed: " + error)
                | Ok strengthProjection ->
                    match!
                        StrengthLifecycle.replayPlans
                            owner
                            ProviderWireDecode.hostMessageId
                            rawMessages
                            durability.LoadFrameBundle
                            strengthProjection
                    with
                    | Error error -> return strengthFailClosed error
                    | Ok plans ->
                        let plans =
                            plans |> List.filter (StrengthLifecycle.needsRawReplay coveredThroughSequence)

                        match plans with
                        | [] -> return []
                        | _ ->
                            let wire = ProviderWireCapture.decodeMessageView rawMessages

                            let snapshot =
                                { CurrentProjection = ProviderProjection.toSemantic wire
                                  CommittedPrefix = None
                                  BlogFrames = []
                                  TransportMessages = Set.empty
                                  HostReanchor = None }

                            let rendered =
                                ProjectionRenderer.renderMessagesWithHostIds
                                    HostDigest.sha256Hex
                                    snapshot
                                    wire.Messages
                                    (StrengthLifecycle.replayIntents plans)

                            match
                                ProjectionMessageEdit.tryApplyRenderedInsertionsPreservingBase
                                    sessionId
                                    HostDigest.sha256Hex
                                    rawMessages
                                    rendered
                            with
                            | Error error -> return strengthFailClosed ("Strength replay render failed: " + error)
                            | Ok replayed ->
                                HostMessageProjection.replaceMessagesInPlace outObj replayed
                                return plans
        }

    /// Host id inside stable provenance g:N/msg:{id}/part:P.
    let private stableHostIdOfProvenance (provenance: string) =
        let marker = "/msg:"
        let start = provenance.IndexOf(marker, StringComparison.Ordinal)

        if start < 0 then
            None
        else
            let idStart = start + marker.Length
            let stop = provenance.IndexOf("/part:", idStart, StringComparison.Ordinal)

            if stop < 0 then
                None
            else
                Some(provenance.Substring(idStart, stop - idStart))

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
                let rec resolveObserved (remaining: XTracePartRef list) (acc: StrengthTraceObservedPart list) =
                    task {
                        match remaining with
                        | [] -> return Ok(List.rev acc)
                        | part :: tail ->
                            match! durable.Writer.BlobWriter.Read part.TextRef with
                            | Error error -> return Error error
                            | Ok body ->
                                return!
                                    resolveObserved
                                        tail
                                        ({ CursorSequence = part.Cursor.Sequence
                                           Kind = part.Kind
                                           ToolName = part.ToolName
                                           Body = body }
                                         :: acc)
                    }

                for plan in plans do
                    if plan.ExistingTraceRange.IsNone then
                        let expectedIds =
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

                        let expectedIdSet = HashSet<string>()

                        for id in expectedIds do
                            ignore (expectedIdSet.Add id)

                        let byStableId =
                            updated.Parts
                            |> List.filter (fun part ->
                                match stableHostIdOfProvenance part.Provenance with
                                | Some id -> expectedIdSet.Contains id
                                | None -> false)

                        let expectedCount = StrengthLifecycle.framePartCount plan.Bundle

                        let stableRange =
                            if List.length byStableId = expectedCount && expectedCount > 0 then
                                let sequences = byStableId |> List.map (fun part -> part.Cursor.Sequence)

                                let first = List.head sequences
                                let last = List.last sequences

                                let contiguous =
                                    sequences
                                    |> List.mapi (fun index value -> value = first + int64 index)
                                    |> List.forall id

                                if contiguous then
                                    Some
                                        { StartInclusive = first
                                          EndExclusive = last + 1L }
                                else
                                    None
                            else
                                None

                        let! range =
                            task {
                                match stableRange with
                                | Some value -> return Ok(Some value)
                                | None ->
                                    match! resolveObserved updated.Parts [] with
                                    | Error error -> return Error error
                                    | Ok observed -> return StrengthTraceRecovery.recoverRange plan.Bundle observed
                            }

                        match range with
                        | Error error -> strengthFailClosed ("Strength Traced recovery failed: " + error)
                        | Ok None ->
                            strengthFailClosed "Strength Promoted frame is absent from XTrace after replay capture"
                        | Ok(Some traced) ->
                            match!
                                durability.Append(
                                    StrengthEvents.traced
                                        plan.Prepared.DecisionId
                                        traced.StartInclusive
                                        traced.EndExclusive
                                )
                            with
                            | StrengthDurableAppend.Applied -> ()
                            | StrengthDurableAppend.SemanticRejected _ -> ()
                            | StrengthDurableAppend.StorageFailed error ->
                                strengthFailClosed ("Strength Traced commit storage failure: " + error)

            | _ -> ()
        }
