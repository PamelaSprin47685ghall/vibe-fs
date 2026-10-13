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
open Wanxiangshu.Context.Trace
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Mission.Planning
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type RelayProjectionDisposition =
    | Unchanged
    | CurrentIteration
    | RetiredAttemptStopped

module RelayNarrativeTransform =
    let private relayRoad (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)
        |> Option.bind (fun relay -> Fold.view relay (RoadId.create (SessionId.value sessionId)))

    let private messageId message =
        ProviderWireDecode.hostMessageId message

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private messageRole message =
        readField (readField message "info") "role"
        |> Option.ofObj
        |> Option.orElseWith (fun () -> readField message "role" |> Option.ofObj)
        |> Option.map (fun value -> unbox<string> value)

    let private messageRoleIsUser message =
        messageRole message
        |> Option.exists (fun role -> role.ToLowerInvariant() = "user")

    let private managerLoopGatePhysical (journal: AgentJournal) (sessionId: SessionId) (retirement: RetirementSummary) =
        let gateKind = ManagerLoopGate.gateKind retirement.Id
        let terminalRun = ProviderRunIdentity.create retirement.ProjectionCut.ProviderRunId

        PromptAuthorityProjectionQueries.activeProfile sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun profile ->
            (PromptDispatcher.forPrompts (PromptJournalAdapter.create journal))
                .GateNudgeAcceptedPhysical
                profile
                PromptAuthority.ContinuationKind.ManagerGuard
                gateKind
                terminalRun)

    let private cutToolIndex (cut: Wanxiangshu.Mission.Relay.ProjectionCut) messages =
        messages
        |> List.tryFindIndex (fun message ->
            ProviderWireDecode.rawPartsOf message
            |> List.choose ProviderWireDecode.decodePart
            |> List.exists (function
                | WireToolCall(toolCallId, _, _)
                | WireToolResult(toolCallId, _) -> ToolCallId.value toolCallId = cut.ToolCallId
                | _ -> false))

    let private activeRetirement (road: RoadView) =
        road.LatestRetirement |> Option.filter (fun _ -> road.ActiveIncumbency.IsSome)

    let private dispositionAfterProjection (road: RoadView) =
        activeRetirement road
        |> Option.map (fun _ -> RelayProjectionDisposition.CurrentIteration)
        |> Option.defaultValue RelayProjectionDisposition.Unchanged

    let private requestBelongsToSuccessor (physical: string option) afterCut freshRoot acceptedRequest gatePhysical =
        match physical with
        | Some current ->
            freshRoot = Some current
            || gatePhysical = Some current
            || (afterCut && acceptedRequest)
        | _ -> false

    let private isSuccessorRequest
        journal
        sessionId
        (road: RoadView)
        (retirement: RetirementSummary)
        acceptedRequest
        messages
        =
        let currentUser =
            messages
            |> List.indexed
            |> List.choose (fun (index, message) ->
                if messageRoleIsUser message then
                    messageId message |> Option.map (fun physical -> index, physical)
                else
                    None)
            |> List.tryLast

        let afterCut =
            match cutToolIndex retirement.ProjectionCut messages, currentUser with
            | Some toolIndex, Some(userIndex, _) -> userIndex > toolIndex
            | _ -> false

        let projection =
            AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
            |> Option.bind (fun session -> session.PromptAuthority)

        let freshRoot =
            projection
            |> Option.bind (fun authority -> authority.ActiveLogicalRun)
            |> Option.map (fun profile -> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)
            |> Option.filter (fun root ->
                road.AuthorityMessageIds
                |> List.exists (fun oldRoot -> PhysicalUserMessageId.value oldRoot = root)
                |> not)

        let physical = currentUser |> Option.map snd

        let gatePhysical =
            managerLoopGatePhysical journal sessionId retirement
            |> Option.map Wanxiangshu.Foundation.Identity.PhysicalUserMessageId.value

        requestBelongsToSuccessor physical afterCut freshRoot acceptedRequest gatePhysical

    let private staleRetirement journal sessionId (road: RoadView) acceptedRequest messages =
        road.LatestRetirement
        |> Option.filter (fun retirement ->
            not (isSuccessorRequest journal sessionId road retirement acceptedRequest messages))

    let private firstCursors (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map XTraceProjection.hostMessageFirstCursors
        |> Option.defaultValue Map.empty

    let private earlierAssistantOrTool openingCursor cursors message =
        match messageRole message, messageId message |> Option.bind (fun id -> Map.tryFind id cursors) with
        | Some role, Some sequence when
            (role.ToLowerInvariant() = "assistant" || role.ToLowerInvariant() = "tool")
            && sequence < openingCursor ->
            true
        | _ -> false

    let private previousRange (work: LifecycleWork) =
        match work.Active, List.tryLast work.Retirements with
        | Some active, Some previous ->
            let ending = previous.RetirementCursor |> Option.defaultValue active.OpeningCursor
            if ending > previous.OpeningCursor then Some(previous.OpeningCursor, ending) else None
        | _ -> None

    let private handoverRecord (work: LifecycleWork) (text: string) : obj =
        box
            {| info =
                {| id = "lwr-prev-" + work.Active.Value.Id
                   sessionID = work.WorkId
                   role = "user"
                   synthetic = true
                   source = "plan-tenure-record" |}
               parts = [| box {| ``type`` = "text"; text = text |} |] |}

    /// Successor visibility. Opening sequence comes from the active lifecycle
    /// tenure. The other side is the first observation of a captured Host
    /// message id. A message absent from that map has not been captured yet,
    /// so it stays. The retirement cut is not a strip condition.
    let private projectActive
        (journal: AgentJournal)
        (sessionId: SessionId)
        (road: RoadView)
        (work: LifecycleWork option)
        outObj
        =
        task {
            let messages = ProviderWireDecode.messagesFromTransformOutput outObj

            match work |> Option.bind (fun lifecycle -> lifecycle.Active |> Option.map (fun active -> lifecycle, active)) with
            | None ->
                HostMessageProjection.replaceMessagesInPlace outObj messages
                return dispositionAfterProjection road
            | Some(lifecycle, active) ->
                let cursors = firstCursors journal sessionId

                let kept =
                    messages
                    |> List.filter (earlierAssistantOrTool active.OpeningCursor cursors >> not)

                let! record =
                    match previousRange lifecycle with
                    | None -> Task.FromResult None
                    | Some(startCursor, endCursor) ->
                        let range =
                            XTraceRange.create
                                (XTraceCursor.create startCursor |> XTraceCursor.nextCursor)
                                (XTraceCursor.create endCursor |> XTraceCursor.nextCursor)

                        LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedFromSnapshot
                            journal
                            (AgentJournal.snapshot journal)
                            sessionId
                            range

                let visible =
                    match record with
                    | Some text when not (String.IsNullOrWhiteSpace text) ->
                        TenureIsolation.insertBeforeCurrentTenure
                            active.OpeningCursor
                            cursors
                            (handoverRecord lifecycle text)
                            kept
                            messageId
                    | _ -> kept

                HostMessageProjection.replaceMessagesInPlace outObj visible
                return dispositionAfterProjection road
        }

    let private project journal (interruptAttempt: SessionId -> Task<unit>) sessionId road acceptedRequest lifecycle outObj =
        task {
            let messages = ProviderWireDecode.messagesFromTransformOutput outObj

            // An active LogicalRun or a claimed loop gate does not identify this
            // physical request: both can coexist with the retired attempt.
            match staleRetirement journal sessionId road acceptedRequest messages with
            | Some _ ->
                do! interruptAttempt sessionId
                HostMessageProjection.replaceMessagesInPlace outObj []
                return RelayProjectionDisposition.RetiredAttemptStopped
            | None -> return! projectActive journal sessionId road lifecycle outObj
        }

    let apply
        (journal: AgentJournal option)
        (acceptedRequest: bool)
        (interruptAttempt: SessionId -> Task<unit>)
        (sessionId: string option)
        (lifecycle: LifecycleWork option)
        (outObj: obj)
        : Task<RelayProjectionDisposition> =
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
            | Some(durable, currentSessionId, road) ->
                return! project durable interruptAttempt currentSessionId road acceptedRequest lifecycle outObj
            | None -> return RelayProjectionDisposition.Unchanged
        }
