namespace Wanxiangshu.Mission.Relay.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Persistence.Journal

module RelayNarrativeTransform =
    let private relayRoad (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)
        |> Option.bind (fun relay -> Fold.view relay (RoadId.create (SessionId.value sessionId)))

    let private messageId message =
        ProviderWireDecode.hostMessageId message

    let private messageContainsToolCall callId message =
        ProviderWireDecode.rawPartsOf message
        |> List.choose ProviderWireDecode.decodePart
        |> List.exists (function
            | WireToolCall(toolCallId, _, _)
            | WireToolResult(toolCallId, _) -> ToolCallId.value toolCallId = callId
            | _ -> false)

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private messageRoleIsUser message =
        let fromInfo = readField (readField message "info") "role"

        let chosen =
            if isNull fromInfo then
                readField message "role"
            else
                fromInfo

        if isNull chosen then
            false
        else
            (unbox<string> chosen).ToLowerInvariant() = "user"

    /// The retired run's own closing tail races the successor prompt send, so
    /// every non-authority message after the retirement tool is dropped until
    /// the next user turn opens. Without a later user turn the whole tail goes.
    /// User turns themselves — prompts, nudges, continuations — always survive
    /// the cut, so the drop converges whether or not the tail already arrived.
    let private postRetirementTailRange cut messages =
        match messages |> List.tryFindIndex (messageContainsToolCall cut.ThroughToolCallId) with
        | None -> None
        | Some toolIndex ->
            let userIndex =
                messages
                |> List.mapi (fun index message -> index, message)
                |> List.tryFind (fun (index, message) -> index > toolIndex && messageRoleIsUser message)
                |> Option.map fst

            Some(toolIndex, userIndex)

    let private inPostRetirementTail tailRange index =
        match tailRange with
        | None -> false
        | Some(toolIndex, None) -> index > toolIndex
        | Some(toolIndex, Some userIndex) -> index > toolIndex && index < userIndex

    /// The successor gate occasion for this retirement is admitted when its
    /// prompt was claimed or accepted. Every legitimate send claims first, so
    /// an unadmitted occasion means this request continues the retired run
    /// itself rather than delivering its successor.
    let private successorGateAdmitted (journal: AgentJournal) (sessionId: SessionId) (retirement: RetirementSummary) =
        let snapshot = AgentJournal.snapshot journal
        let gateKind = RelaySuccessorGate.gateKind retirement.Id

        let terminalRun =
            ProviderRunIdentity.create retirement.ProjectionCut.ThroughProviderRunId

        match PromptAuthorityLedger.activeProfile sessionId snapshot.AgentProjections with
        | None -> false
        | Some profile ->
            let runtime = PromptDispatcher.forJournal journal
            runtime.GateNudgeAlreadyAdmitted profile PromptAuthority.ContinuationKind.ManagerGuard gateKind terminalRun

    let private cutMessages authorityMessageIds (cut: ProjectionCut) messages =
        match
            messages
            |> List.tryFindIndex (fun message -> messageId message = Some cut.ThroughProviderRunId)
        with
        | None -> Error("relay projection cut provider run is absent: " + cut.ThroughProviderRunId)
        | Some cutIndex ->
            let stale = cut.StaleProviderRunIds |> Set.ofList
            let tailRange = postRetirementTailRange cut messages

            messages
            |> List.mapi (fun index message -> index, message)
            |> List.choose (fun (index, message) ->
                let id = messageId message

                let keepAuthority =
                    id |> Option.exists (fun value -> Set.contains value authorityMessageIds)

                let oldEpoch = index <= cutIndex && not keepAuthority
                let staleRun = id |> Option.exists (fun value -> Set.contains value stale)
                let retirementTool = messageContainsToolCall cut.ThroughToolCallId message

                let postRetirementTail = inPostRetirementTail tailRange index

                if keepAuthority then
                    Some message
                elif
                    oldEpoch
                    || staleRun
                    || retirementTool
                    || (postRetirementTail && not keepAuthority)
                then
                    None
                else
                    Some message)
            |> Ok

    let private phaseName =
        function
        | IncumbencyPhase.AuditPending -> "AuditPending"
        | IncumbencyPhase.WorkOwned -> "WorkOwned"
        | IncumbencyPhase.PerfectAwaitingRetirement -> "PerfectAwaitingRetirement"
        | IncumbencyPhase.RetirementCleanupBlocked -> "RetirementCleanupBlocked"

    let private batonText (road: RoadView) =
        let latest =
            road.LatestRetirement |> Option.map (fun retirement -> retirement.Baton)

        let values =
            [ "authority_revision=" + AuthorityRevision.value road.AuthorityRevision
              "incumbency_id="
              + (road.ActiveIncumbency
                 |> Option.map IncumbencyId.value
                 |> Option.defaultValue "none")
              "phase="
              + (road.ActivePhase |> Option.map phaseName |> Option.defaultValue "Retired")
              "open_quality_obligations="
              + (latest
                 |> Option.map (fun baton -> String.concat "," baton.OpenObligations)
                 |> Option.defaultValue "")
              "evidence_refs="
              + (latest
                 |> Option.map (fun baton -> String.concat "," baton.EvidenceRefs)
                 |> Option.defaultValue "") ]

        String.concat "\n" ([ "[RelayContext]" ] @ values @ [ "[/RelayContext]" ])

    let private syntheticContext (sessionId: SessionId) road =
        let identity =
            road.ActiveIncumbency
            |> Option.map IncumbencyId.value
            |> Option.defaultValue "retired"

        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box ("relay-context:" + identity)
                        "sessionID", box (SessionId.value sessionId)
                        "role", box "user" ]
              )
              "role", box "user"
              "parts", box [| createObj [ "type", box "text"; "text", box (batonText road) ] |] ]

    let private projectMessages authorityMessageIds road outObj =
        let messages = ProviderWireDecode.messagesFromTransformOutput outObj

        let projected =
            match road.ActiveSource, road.LatestRetirement with
            | Some(BatonSource.Retirement predecessor), Some retirement when retirement.Id = predecessor ->
                cutMessages authorityMessageIds retirement.ProjectionCut messages
            | _ -> Ok messages

        match projected with
        | Error error -> raise (InvalidOperationException error)
        | Ok current -> current

    let private project journal (interruptAttempt: SessionId -> Task<unit>) sessionId road outObj =
        task {
            // A committed retirement with no admitted successor prompt means this
            // request continues the retired run itself: interrupt the attempt in the
            // transform hook so the run is pinched off before emitting any network request.
            match road.LatestRetirement with
            | Some retirement when not (successorGateAdmitted journal sessionId retirement) ->
                do! interruptAttempt sessionId
                HostMessageProjection.replaceMessagesInPlace outObj []
            | _ ->
                let authorityMessageIds =
                    road.AuthorityMessageIds |> List.map PhysicalUserMessageId.value |> Set.ofList

                let current = projectMessages authorityMessageIds road outObj
                HostMessageProjection.replaceMessagesInPlace outObj (syntheticContext sessionId road :: current)
        }

    let apply
        (journal: AgentJournal option)
        (interruptAttempt: SessionId -> Task<unit>)
        (sessionId: string option)
        (outObj: obj)
        : Task<unit> =
        task {
            let resolved =
                journal
                |> Option.bind (fun durable ->
                    sessionId
                    |> Option.filter (fun value -> not (String.IsNullOrWhiteSpace value))
                    |> Option.bind (fun value ->
                        let sid = SessionId.create value
                        relayRoad durable sid |> Option.map (fun road -> durable, sid, road)))

            match resolved with
            | Some(durable, currentSessionId, road) -> do! project durable interruptAttempt currentSessionId road outObj
            | None -> ()
        }
