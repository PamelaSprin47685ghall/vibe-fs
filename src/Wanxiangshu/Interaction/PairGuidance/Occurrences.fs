namespace Wanxiangshu.Interaction.PairGuidance

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Domain
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Persistence.Journal.JournalOutcome
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
module PairGuidanceOccurrences =

    let source = "pair-programming-auto-injected"

    let private idPrefix = "pair-programming-auto-injected-"

    let private transcriptKey (sessionId: string option) : string = defaultArg sessionId ""

    let stableCallId (sessionId: string option) (ordinal: int64) : string =
        let digest =
            HostDigest.sha256Hex ((transcriptKey sessionId) + source + string ordinal)

        idPrefix + digest.Substring(0, 24)

    let skipRequested () : bool =
        match Environment.GetEnvironmentVariable "WANXIANGSHU_SKIP_AUTO_INJECTED" with
        | "1" -> true
        | _ -> false

    /// Process-local ledger when no journal is available; append-only within the process.
    let private memoryLedger = Dictionary<string, ResizeArray<PairGuidanceOccurrence>>()

    let private occurrenceOf (pair: PairProgrammingGuideline) : PairGuidanceOccurrence =
        { Ordinal = pair.Ordinal
          CallId = ToolCallId.value pair.CallId
          MarkerText = pair.MarkerText
          CallGap = pair.CallGap
          ResultGap = pair.ResultGap }

    let private durablePairs
        (select: GuidelineProjectionState -> PairProgrammingGuideline list)
        (journal: AgentJournal)
        (sessionId: SessionId)
        : PairGuidanceOccurrence list =
        match AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections with
        | None -> []
        | Some session ->
            session.Guidelines
            |> Option.map select
            |> Option.defaultValue []
            |> List.map occurrenceOf

    let private appendDurable
        (journal: AgentJournal)
        (sessionId: SessionId)
        (occurrence: PairGuidanceOccurrence)
        : Task<Result<unit, string>> =
        task {
            let fact =
                HostFact.PairProgrammingGuidelineAnchored
                    {| SessionId = sessionId
                       Ordinal = occurrence.Ordinal
                       CallId = ToolCallId.create occurrence.CallId
                       MarkerText = occurrence.MarkerText
                       CallGap = occurrence.CallGap
                       ResultGap = occurrence.ResultGap |}

            match! AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let private readMemory (key: string) : PairGuidanceOccurrence list =
        match memoryLedger.TryGetValue key with
        | true, pairs -> pairs |> Seq.toList
        | false, _ -> []

    let private appendMemory (key: string) (occurrence: PairGuidanceOccurrence) : Task<Result<unit, string>> =
        match memoryLedger.TryGetValue key with
        | true, pairs -> pairs.Add occurrence
        | false, _ ->
            // DSL-MUTABLE: algorithm-scratch — new pair list for dictionary insert
            let pairs = ResizeArray<PairGuidanceOccurrence>()
            pairs.Add occurrence
            memoryLedger.[key] <- pairs

        Task.FromResult(Ok())

    let ledger (journal: AgentJournal option) (sessionId: string option) : PairGuidanceLedger =
        match journal, sessionId with
        | Some durable, Some sid when not (String.IsNullOrWhiteSpace sid) ->
            let session = SessionId.create sid

            { History = durablePairs GuidelineProjection.pairs durable session
              Visible = durablePairs GuidelineProjection.visiblePairs durable session
              Append = appendDurable durable session }
        | _ ->
            let key = transcriptKey sessionId
            let memory = readMemory key

            { History = memory
              Visible = memory
              Append = appendMemory key }

    let private sessionRole (durable: AgentJournal) (sessionId: SessionId) : Role option =
        let projections = (AgentJournal.snapshot durable).AgentProjections

        PromptAuthorityProjectionQueries.activeProfile sessionId projections
        |> Option.orElseWith (fun () -> PromptAuthorityProjectionQueries.lastAuthorityProfile sessionId projections)
        |> Option.map (fun profile -> profile.CanonicalRole)
        |> Option.orElseWith (fun () ->
            projections.HandleByChildSession
            |> Map.tryFind sessionId
            |> Option.map (fun handle -> handle.CanonicalRole))

    let isInternalSession (journal: AgentJournal option) (sessionId: string option) : bool =
        match journal, sessionId with
        | Some durable, Some sid when not (String.IsNullOrWhiteSpace sid) ->
            let session = SessionId.create sid
            let snapshot = AgentJournal.snapshot durable

            SessionAssociationProjection.isCompanion session snapshot.AgentProjections.Associations
            || (sessionRole durable session |> Option.exists Roles.isInternal)
        | _ -> false

    let private nextOrdinal (history: PairGuidanceOccurrence list) =
        match history with
        | [] -> 1L
        | pairs -> (List.last pairs).Ordinal + 1L

    let private occupied (visible: PairGuidanceOccurrence list) (callGap: TranscriptGap) (resultGap: TranscriptGap) =
        visible
        |> List.exists (fun pair -> pair.CallGap = callGap && pair.ResultGap = resultGap)

    let private orphanCheck (ledger: PairGuidanceLedger) (transcript: PairGuidanceTranscript) =
        let known = ledger.History |> List.map (fun pair -> pair.CallId) |> Set.ofList

        let orphaned =
            transcript.SyntheticCallIds
            |> List.filter (fun callId -> not (Set.contains callId known))

        Result.requireTrue
            (sprintf
                "synthetic messages without durable record (callId %s, HOST-013)"
                (String.Join(", ", orphaned |> List.truncate 3)))
            (List.isEmpty orphaned)

    let private commitAt
        (ledger: PairGuidanceLedger)
        (sessionId: string option)
        (markerText: string)
        (render: PairGuidanceOccurrence list -> Result<'Wire, string>)
        (callGap: TranscriptGap)
        (resultGap: TranscriptGap)
        : Task<Result<'Wire * bool, string>> =
        taskResult {
            if occupied ledger.Visible callGap resultGap || skipRequested () then
                let! replayed = render ledger.Visible
                return (replayed, false)
            else
                let ordinal = nextOrdinal ledger.History

                let candidate =
                    { Ordinal = ordinal
                      CallId = stableCallId sessionId ordinal
                      MarkerText = markerText
                      CallGap = callGap
                      ResultGap = resultGap }

                let! rendered = render (ledger.Visible @ [ candidate ])
                do! ledger.Append candidate
                return (rendered, true)
        }

    /// Internal transcripts never carry a new occurrence, whatever the host decoded.
    let private placementOf (internalTranscript: bool) (transcript: PairGuidanceTranscript) =
        if internalTranscript then Ok None else transcript.Carrier

    let occur
        (ledger: PairGuidanceLedger)
        (sessionId: string option)
        (internalSession: bool)
        (markerText: string)
        (transcript: PairGuidanceTranscript)
        (render: PairGuidanceOccurrence list -> Result<'Wire, string>)
        : Task<Result<'Wire * bool, string>> =
        taskResult {
            do! orphanCheck ledger transcript
            let! placement = placementOf (internalSession || transcript.InternalAuthor) transcript

            match placement with
            | None ->
                let! replayed = render ledger.Visible
                return (replayed, false)
            | Some(callGap, resultGap) -> return! commitAt ledger sessionId markerText render callGap resultGap
        }
