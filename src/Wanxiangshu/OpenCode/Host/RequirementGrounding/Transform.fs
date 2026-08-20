namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

module RequirementGroundingTransform =

    let source = "requirement-grounding-auto-read"
    let toolName = "read"
    let cursorSeparator = PairProgrammingThoughtTransform.cursorGuidanceSeparator

    let private tryString value =
        if isNull value then None else Some(string value)

    let private sourceOf raw =
        if isNull raw || isNull raw?info || isNull raw?info?source then
            None
        else
            tryString raw?info?source

    let isGroundingRead raw = sourceOf raw = Some source

    let private callIdOf raw =
        let resolveParts () =
            let parts = unbox<obj array> raw?parts

            if parts.Length = 0 then None
            elif isNull parts.[0]?callID then None
            else Some(string parts.[0]?callID)

        if isNull raw || isNull raw?parts then
            None
        else
            resolveParts ()

    let stableCallId sessionId workspace packageName digest ordinal index =
        let input =
            String.concat "\u0000" [ sessionId; workspace; packageName; digest; string ordinal; string index ]

        "requirement-grounding-read-" + (HostDigest.sha256Hex input).Substring(0, 24)

    let private escapeAttribute (value: string) =
        value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&apos;")

    let cursorResult path resultBytes =
        "<requirement_read path=\""
        + escapeAttribute path
        + "\">\n"
        + resultBytes
        + (if resultBytes.EndsWith("\n", StringComparison.Ordinal) then
               ""
           else
               "\n")
        + "</requirement_read>"

    let private buildReadMessage (read: RequirementGroundingAnchoredRead) : obj =
        let input = createObj [ "filePath", box read.Path ]

        let part =
            createObj
                [ "type", box "tool"
                  "tool", box toolName
                  "callID", box (ToolCallId.value read.CallId)
                  "state",
                  box (
                      createObj
                          [ "status", box "completed"
                            "input", box input
                            "output", box read.ResultBytes
                            "time", box (createObj [ "start", box 0; "end", box 0 ]) ]
                  ) ]

        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box (ToolCallId.value read.CallId)
                        "role", box "assistant"
                        "source", box source
                        "synthetic", box true ]
              )
              "parts", box [| part |] ]

    let private gapPresent addresses gap =
        match gap with
        | TranscriptGap.Start -> true
        | TranscriptGap.Before address
        | TranscriptGap.After address -> Set.contains (TranscriptMessageAddress.value address) addresses

    let private placeable addresses occurrence =
        gapPresent addresses occurrence.CallGap
        && gapPresent addresses occurrence.ResultGap

    let private occurrenceReads occurrences predicate =
        occurrences |> List.filter predicate |> List.collect _.Reads

    let private appendReads (output: ResizeArray<obj>) reads =
        reads |> List.iter (buildReadMessage >> output.Add)

    let private appendAroundMessage active (output: ResizeArray<obj>) message =
        match ProviderWireDecode.hostMessageId message with
        | None -> output.Add message
        | Some address ->
            appendReads
                output
                (occurrenceReads active (fun occurrence ->
                    occurrence.ResultGap = TranscriptGap.Before(TranscriptMessageAddress.create address)))

            output.Add message

            appendReads
                output
                (occurrenceReads active (fun occurrence ->
                    occurrence.ResultGap = TranscriptGap.After(TranscriptMessageAddress.create address)))

    let private replayOrdinary realMessages occurrences =
        let addresses =
            realMessages |> List.choose ProviderWireDecode.hostMessageId |> Set.ofList

        let active = occurrences |> List.filter (placeable addresses)
        let output = ResizeArray<obj>()
        appendReads output (occurrenceReads active (fun occurrence -> occurrence.ResultGap = TranscriptGap.Start))

        for message in realMessages do
            appendAroundMessage active output message

        Seq.toList output

    let private cursorSuffixesAfter active message =
        match ProviderWireDecode.hostMessageId message with
        | None -> []
        | Some address ->
            occurrenceReads active (fun occurrence ->
                occurrence.ResultGap = TranscriptGap.After(TranscriptMessageAddress.create address))
            |> List.map _.CursorResultBytes

    let private projectCursorMessage active message =
        let suffixes = cursorSuffixesAfter active message

        if List.isEmpty suffixes then
            message
        else
            PairProgrammingThoughtTransform.appendCursorSuffixes suffixes message
            |> Option.defaultValue message

    let private replayCursor realMessages occurrences =
        let addresses =
            realMessages |> List.choose ProviderWireDecode.hostMessageId |> Set.ofList

        let active = occurrences |> List.filter (placeable addresses)

        realMessages |> List.map (projectCursorMessage active)

    let private replay providerId realMessages occurrences =
        if PairProgrammingThoughtTransform.isCursorProvider providerId then
            replayCursor realMessages occurrences
        else
            replayOrdinary realMessages occurrences

    let private argsJson path =
        CanonicalJson.canonicalJson (createObj [ "filePath", box path ])

    let private anchoredReads sessionId ordinal snapshot =
        snapshot.Materials
        |> List.mapi (fun index material ->
            { CallId =
                ToolCallId.create (
                    stableCallId sessionId snapshot.Workspace snapshot.PackageName snapshot.Digest ordinal index
                )
              Path = material.Path
              ArgsJson = argsJson material.Path
              ResultBytes = material.ResultBytes
              CursorResultBytes = cursorResult material.Path material.ResultBytes })

    let private occurrence sessionId ordinal callGap resultGap snapshot =
        { Workspace = snapshot.Workspace
          PackageName = snapshot.PackageName
          Digest = snapshot.Digest
          Ordinal = ordinal
          Reads = anchoredReads sessionId ordinal snapshot
          CallGap = callGap
          ResultGap = resultGap }

    let private appendOneRequested journal sessionId callGap resultGap snapshot =
        let next =
            RequirementGroundingRuntime.nextOrdinal journal (SessionId.create sessionId)

        let value = occurrence sessionId next callGap resultGap snapshot

        taskResult {
            let! _ =
                RequirementGroundingRuntime.appendAnchored journal (SessionId.create sessionId) value
                |> TaskResult.mapError JournalAppendFailure.describe

            return ()
        }

    let private appendRequested journal sessionId callGap resultGap snapshots =
        let rec loop remaining =
            match remaining with
            | [] -> Task.FromResult(Ok())
            | snapshot :: tail ->
                taskResult {
                    do! appendOneRequested journal sessionId callGap resultGap snapshot
                    return! loop tail
                }

        loop snapshots

    let private validateSyntheticHistory history rawMessages =
        let knownCallIds =
            history
            |> List.collect _.Reads
            |> List.map (_.CallId >> ToolCallId.value)
            |> Set.ofList

        let orphaned =
            rawMessages
            |> List.filter isGroundingRead
            |> List.choose callIdOf
            |> List.filter (fun callId -> not (Set.contains callId knownCallIds))

        if List.isEmpty orphaned then
            Ok(rawMessages |> List.filter (isGroundingRead >> not))
        else
            Error(
                "synthetic grounding reads without durable record: "
                + String.Join(", ", orphaned)
            )

    let private stripCursorHistory history rawMessages =
        let providerId = PairProgrammingThoughtTransform.providerIdFromMessages rawMessages

        if PairProgrammingThoughtTransform.isCursorProvider providerId then
            let suffixes = history |> List.collect _.Reads |> List.map _.CursorResultBytes

            rawMessages
            |> List.map (PairProgrammingThoughtTransform.stripCursorSuffixes suffixes)
        else
            rawMessages

    let private anchorRequested journal sessionId realMessages providerId history pending =
        if List.isEmpty pending then
            Task.FromResult(Ok(replay providerId realMessages history))
        else
            taskResult {
                let! callGap, resultGap = PairProgrammingThoughtTransform.decideCurrentPlacement realMessages
                do! appendRequested journal sessionId callGap resultGap pending

                let committed =
                    RequirementGroundingRuntime.occurrences journal (SessionId.create sessionId)

                return replay providerId realMessages committed
            }

    let tryProject
        (journal: AgentJournal)
        (sessionId: string)
        (rawMessages: obj list)
        : Task<Result<obj list, string>> =
        taskResult {
            let session = SessionId.create sessionId
            let history = RequirementGroundingRuntime.historyOccurrences journal session
            let visibleHistory = RequirementGroundingRuntime.occurrences journal session
            let! realMessages = validateSyntheticHistory history (stripCursorHistory history rawMessages)
            let providerId = PairProgrammingThoughtTransform.providerIdFromMessages realMessages
            let pending = RequirementGroundingRuntime.pending journal session
            return! anchorRequested journal sessionId realMessages providerId visibleHistory pending
        }

    /// REQUIREMENT-GROUNDING-007/012: project requirement grounding or terminate on failure.
    /// Domain decision: projection failure terminates the session.
    let projectOrTerminate
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (projectionSessionIdOpt: string option)
        (terminateSession: string -> string -> Task<Result<unit, string>>)
        (outObj: obj)
        : Task =
        let applyProjection sessionId projected =
            match projected with
            | Ok values ->
                HostMessageProjection.replaceMessagesInPlace outObj values
                Task.FromResult()
            | Error reason ->
                Diagnostic.emit
                    "requirement-grounding-projection-fail-closed"
                    [ "session_id", sessionId; "result", reason ]

                task {
                    let! _ = terminateSession sessionId reason
                    ()
                }

        match journal, workspaceDirectory, projectionSessionIdOpt with
        | Some durable, Some _, Some sessionId when not (String.IsNullOrWhiteSpace sessionId) ->
            task {
                let messages = unbox<obj array> outObj?messages |> Array.toList

                let! projected = tryProject durable sessionId messages

                do! applyProjection sessionId projected
            }
        | _ -> Task.FromResult()
