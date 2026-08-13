namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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
        : StrengthReplayPlan list =
        match strengthDurability with
        | None -> []
        | Some durability ->
            let owner = SessionId.create sessionId
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput outObj

            let coveredThroughSequence =
                journal
                |> Option.bind (fun durable ->
                    AgentProjection.tryFind owner (AgentJournal.snapshot durable).AgentProjections
                    |> Option.bind (fun state -> state.Blog)
                    |> Option.map (fun blog -> blog.Coverage.IngestedThroughSequence))

            match durability.LoadProjection() with
            | Error error -> strengthFailClosed ("Strength replay projection failed: " + error)
            | Ok strengthProjection ->
                match
                    StrengthLifecycle.replayPlans
                        owner
                        ProviderWireDecode.hostMessageId
                        rawMessages
                        durability.LoadFrameBundle
                        strengthProjection
                with
                | Error error -> strengthFailClosed error
                | Ok plans ->
                    let plans =
                        plans |> List.filter (StrengthLifecycle.needsRawReplay coveredThroughSequence)

                    match plans with
                    | [] -> []
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
                        | Error error -> strengthFailClosed ("Strength replay render failed: " + error)
                        | Ok replayed ->
                            HostMessageProjection.replaceMessagesInPlace outObj replayed
                            plans

    /// Close Promoted → Traced after XTrace capture for plans that lacked a
    /// prior trace range. Stable Host ids recover the exact range; legacy
    /// positional traces fall back to unique canonical match (fail closed).
        /// Stable capture writes `g:N/msg:{hostId}/part:P`. Extract the Host id
    /// so Promoted→Traced close can HashSet-lookup instead of scanning
    /// expectedIds per part.
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

let commitTracedAfterCapture
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailClosed: string -> unit)
        (traceState: XTraceProjectionState option)
        (plans: StrengthReplayPlan list)
        : unit =
        match journal, strengthDurability, traceState with
        | Some durable, Some durability, Some updated ->
            let rec resolveObserved (remaining: XTracePartRef list) (acc: StrengthTraceObservedPart list) =
                match remaining with
                | [] -> Ok(List.rev acc)
                | part :: tail ->
                    match durable.Writer.BlobWriter.Read part.TextRef with
                    | Error error -> Error error
                    | Ok body ->
                        resolveObserved
                            tail
                            ({ CursorSequence = part.Cursor.Sequence
                               Kind = part.Kind
                               ToolName = part.ToolName
                               Body = body }
                             :: acc)

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

                    let range =
                        match stableRange with
                        | Some value -> Ok(Some value)
                        | None ->
                            resolveObserved updated.Parts []
                            |> Result.bind (StrengthTraceRecovery.recoverRange plan.Bundle)

                    match range with
                    | Error error -> strengthFailClosed ("Strength Traced recovery failed: " + error)
                    | Ok None -> strengthFailClosed "Strength Promoted frame is absent from XTrace after replay capture"
                    | Ok(Some traced) ->
                        match
                            durability.Append(
                                StrengthEvents.traced plan.Prepared.DecisionId traced.StartInclusive traced.EndExclusive
                            )
                        with
                        | Ok() -> ()
                        | Error error -> strengthFailClosed ("Strength Traced commit failed closed: " + error)
        | _ -> ()
