namespace Wanxiangshu.Mission.Work.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Work
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

    let private cutToolIndex (cutToolCallId: string) messages =
        messages
        |> List.tryFindIndex (fun message ->
            ProviderWireDecode.rawPartsOf message
            |> List.choose ProviderWireDecode.decodePart
            |> List.exists (function
                | WireToolCall(toolCallId, _, _)
                | WireToolResult(toolCallId, _) -> ToolCallId.value toolCallId = cutToolCallId
                | _ -> false))

    let private dispositionAfterProjection (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork option) =
        match work with
        | Some lifecycle when lifecycle.Active.IsSome && not (List.isEmpty lifecycle.Retirements) ->
            RelayProjectionDisposition.CurrentIteration
        | _ -> RelayProjectionDisposition.Unchanged

    let private requestBelongsToSuccessor (physical: string option) afterCut freshRoot acceptedRequest gatePhysical =
        match physical with
        | Some current ->
            freshRoot = Some current
            || gatePhysical = Some current
            || (afterCut && acceptedRequest)
        | _ -> false

    /// The retired attempt is decided from the unified lifecycle work: an S4
    /// work whose current tenure was Relayed, with a durable retirement cut.
    /// The request is stale unless it belongs to the successor (the loop-gate
    /// nudge, or an accepted request observed after the cut).
    let private staleRetirement
        journal
        sessionId
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        acceptedRequest
        messages
        =
        if
            work.Sequence <> Wanxiangshu.Mission.Lifecycle.WorkSequence.Execution
            || not work.CurrentRelayed
        then
            None
        else
            work.Retirements
            |> List.tryFindBack (fun record ->
                record.Outcome = Wanxiangshu.Mission.Lifecycle.RetirementOutcome.Relayed)
            |> Option.bind (fun record -> record.ProjectionCut)
            |> Option.filter (fun cut ->
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
                    match cutToolIndex cut.ToolCallId messages, currentUser with
                    | Some toolIndex, Some(userIndex, _) -> userIndex > toolIndex
                    | _ -> false

                let physical = currentUser |> Option.map snd

                let gatePhysical =
                    work.Retirements
                    |> List.tryFindBack (fun record ->
                        record.Outcome = Wanxiangshu.Mission.Lifecycle.RetirementOutcome.Relayed)
                    |> Option.bind (fun record ->
                        PromptAuthorityProjectionQueries.activeProfile
                            sessionId
                            (AgentJournal.snapshot journal).AgentProjections
                        |> Option.bind (fun profile ->
                            (PromptDispatcher.forPrompts (PromptJournalAdapter.create journal))
                                .GateNudgeAcceptedPhysical
                                profile
                                PromptAuthority.ContinuationKind.ManagerGuard
                                (ManagerLoopGate.gateKind (RetirementId.create record.RetirementId))
                                (ProviderRunIdentity.create cut.ProviderRunId)))
                    |> Option.map Wanxiangshu.Foundation.Identity.PhysicalUserMessageId.value

                not (requestBelongsToSuccessor physical afterCut None acceptedRequest gatePhysical))

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
        (work: LifecycleWork option)
        outObj
        =
        task {
            let messages = ProviderWireDecode.messagesFromTransformOutput outObj

            match work |> Option.bind (fun lifecycle -> lifecycle.Active |> Option.map (fun active -> lifecycle, active)) with
            | None ->
                HostMessageProjection.replaceMessagesInPlace outObj messages
                return dispositionAfterProjection work
            | Some(lifecycle, active) ->
                let cursors = firstCursors journal sessionId

                // The first tenure has no predecessor to strip. Only a successor
                // strips the assistant/tool messages observed before its opening.
                let kept =
                    if List.isEmpty lifecycle.Retirements then
                        messages
                    else
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
                return dispositionAfterProjection work
        }

    let private project
        journal
        (interruptAttempt: SessionId -> Task<unit>)
        sessionId
        acceptedRequest
        (lifecycle: LifecycleWork option)
        outObj
        =
        task {
            let messages = ProviderWireDecode.messagesFromTransformOutput outObj

            // An active LogicalRun or a claimed loop gate does not identify this
            // physical request: both can coexist with the retired attempt.
            let stale =
                lifecycle
                |> Option.bind (fun work -> staleRetirement journal sessionId work acceptedRequest messages)

            match stale with
            | Some _ ->
                do! interruptAttempt sessionId
                HostMessageProjection.replaceMessagesInPlace outObj []
                return RelayProjectionDisposition.RetiredAttemptStopped
            | None -> return! projectActive journal sessionId lifecycle outObj
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
            // No unified work means this session is not on the lifecycle track:
            // the projection must leave the message set untouched rather than
            // re-encode it.
            match journal, sessionId, lifecycle with
            | Some durable, Some value, Some work when not (String.IsNullOrWhiteSpace value) ->
                return!
                    project
                        durable
                        interruptAttempt
                        (SessionId.create value)
                        acceptedRequest
                        (Some work)
                        outObj
            | _ -> return RelayProjectionDisposition.Unchanged
        }
