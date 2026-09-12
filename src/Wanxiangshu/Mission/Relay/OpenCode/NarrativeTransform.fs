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

    let private isAuthorityMessage authorityMessageIds message =
        messageId message
        |> Option.exists (fun value -> Set.contains value authorityMessageIds)

    let private partCallsTool callId =
        function
        | WireToolCall(toolCallId, _, _)
        | WireToolResult(toolCallId, _) -> ToolCallId.value toolCallId = callId
        | _ -> false

    let private messageContainsToolCall callId message =
        ProviderWireDecode.rawPartsOf message
        |> List.choose ProviderWireDecode.decodePart
        |> List.exists (partCallsTool callId)

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

    /// The retired run's own closing tail races the loop wake send, so every
    /// non-authority message after the retirement tool is dropped through the
    /// first non-authority user continuation used only to wake the loop.
    /// Without a later wake turn the whole tail goes. Typed authority turns
    /// always survive the cut, so the drop converges whether or not the tail
    /// already arrived. The cut only recognises position and role, never text.
    /// The retired epoch itself ends at the retirement tool call: the caller
    /// has already required both exact cut positions, so the known toolIndex
    /// arrives without a second search.
    let private isLoopWakeCandidate toolIndex authorityMessageIds (index, message) =
        index > toolIndex
        && messageRoleIsUser message
        && not (isAuthorityMessage authorityMessageIds message)

    let private postRetirementTailRange toolIndex authorityMessageIds messages =
        let wakeIndex =
            messages
            |> List.mapi (fun index message -> index, message)
            |> List.tryFind (isLoopWakeCandidate toolIndex authorityMessageIds)
            |> Option.map fst

        Some(toolIndex, wakeIndex)

    let private inPostRetirementTail tailRange index =
        tailRange
        |> Option.exists (fun (toolIndex, wakeIndex) ->
            index > toolIndex && wakeIndex |> Option.forall (fun wake -> index <= wake))

    /// The manager-loop gate occasion for this retirement is admitted when its
    /// prompt was claimed or accepted. Every legitimate send claims first, so
    /// an unadmitted occasion means this request continues the retired run
    /// itself rather than delivering the next iteration.
    let private managerLoopGateAdmitted (journal: AgentJournal) (sessionId: SessionId) (retirement: RetirementSummary) =
        let gateKind = ManagerLoopGate.gateKind retirement.Id
        let terminalRun = ProviderRunIdentity.create retirement.ProjectionCut.ProviderRunId

        PromptAuthorityProjectionQueries.activeProfile sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.exists (fun profile ->
            (PromptDispatcher.forPrompts (PromptJournalAdapter.create journal))
                .GateNudgeAlreadyAdmitted
                profile
                PromptAuthority.ContinuationKind.ManagerGuard
                gateKind
                terminalRun)

    let private isRetiredRunMessage retiredRunIds message =
        messageId message
        |> Option.exists (fun value -> Set.contains value retiredRunIds)

    let private isRetirementToolMessage (cut: ProjectionCut) message =
        messageContainsToolCall cut.ToolCallId message

    let private isRetiredEpoch toolIndex index keepAuthority = index <= toolIndex && not keepAuthority

    let private isCutDrop (cut: ProjectionCut) toolIndex tailRange retiredRunIds index message keepAuthority =
        isRetiredEpoch toolIndex index keepAuthority
        || isRetiredRunMessage retiredRunIds message
        || isRetirementToolMessage cut message
        || inPostRetirementTail tailRange index

    let private shouldKeepMessage authorityMessageIds cut toolIndex tailRange retiredRunIds (index, message) =
        let keepAuthority = isAuthorityMessage authorityMessageIds message

        keepAuthority
        || not (isCutDrop cut toolIndex tailRange retiredRunIds index message keepAuthority)

    let private keepCutMessage authorityMessageIds cut toolIndex tailRange retiredRunIds (index, message) =
        if shouldKeepMessage authorityMessageIds cut toolIndex tailRange retiredRunIds (index, message) then
            Some message
        else
            None

    let private cutProviderRunPresent (cut: ProjectionCut) messages =
        messages
        |> List.exists (fun message -> messageId message = Some cut.ProviderRunId)

    let private cutToolIndex (cut: ProjectionCut) messages =
        messages |> List.tryFindIndex (messageContainsToolCall cut.ToolCallId)

    let private requireCutPresent (cut: ProjectionCut) messages =
        if cutProviderRunPresent cut messages then
            Ok()
        else
            Error("relay projection cut provider run is absent: " + cut.ProviderRunId)

    let private requireCutToolIndex (cut: ProjectionCut) messages =
        requireCutPresent cut messages
        |> Result.bind (fun () ->
            cutToolIndex cut messages
            |> Option.map (fun toolIndex -> Ok toolIndex: Result<int, string>)
            |> Option.defaultWith (fun () -> Error("relay projection cut tool call is absent: " + cut.ToolCallId)))

    let private applyCut authorityMessageIds (cut: ProjectionCut) retiredRunIds messages toolIndex =
        let tailRange = postRetirementTailRange toolIndex authorityMessageIds messages

        messages
        |> List.mapi (fun index message -> index, message)
        |> List.choose (keepCutMessage authorityMessageIds cut toolIndex tailRange retiredRunIds)

    let private cutMessages authorityMessageIds (cut: ProjectionCut) (retiredRunIds: Set<string>) messages =
        requireCutToolIndex cut messages
        |> Result.map (applyCut authorityMessageIds cut retiredRunIds messages)

    let private activeRetirement (road: RoadView) =
        road.LatestRetirement |> Option.filter (fun _ -> road.ActiveIncumbency.IsSome)

    let private projectMessages authorityMessageIds (road: RoadView) messages =
        // The cut applies whenever a retirement is followed by an active
        // iteration, regardless of outcome: Continue opens the next iteration
        // automatically, while an Accepted retirement only gains one when
        // Change explicitly opens it after invalidating the certificate.
        activeRetirement road
        |> Option.map (fun retirement ->
            cutMessages authorityMessageIds retirement.ProjectionCut road.RetiredProviderRunIds messages)
        |> Option.defaultWith (fun () -> Ok messages)

    let private authorityIdSet (road: RoadView) =
        road.AuthorityMessageIds |> List.map PhysicalUserMessageId.value |> Set.ofList

    let private dispositionAfterProjection (road: RoadView) =
        activeRetirement road
        |> Option.map (fun _ -> RelayProjectionDisposition.CurrentIteration)
        |> Option.defaultValue RelayProjectionDisposition.Unchanged

    let private isUnadmittedContinuation journal sessionId (road: RoadView) (retirement: RetirementSummary) =
        road.ActiveIncumbency.IsNone
        || not (managerLoopGateAdmitted journal sessionId retirement)

    let private staleRetirement journal sessionId (road: RoadView) =
        road.LatestRetirement
        |> Option.filter (isUnadmittedContinuation journal sessionId road)

    let private projectActive (road: RoadView) outObj =
        let authorityMessageIds = authorityIdSet road
        let messages = ProviderWireDecode.messagesFromTransformOutput outObj

        let current =
            projectMessages authorityMessageIds road messages
            |> Result.defaultWith (fun error -> raise (InvalidOperationException error))

        HostMessageProjection.replaceMessagesInPlace outObj current
        dispositionAfterProjection road

    let private project journal (interruptAttempt: SessionId -> Task<unit>) sessionId road outObj =
        task {
            // A retirement with no active admitted loop prompt means this request
            // continues the retired run itself. Interrupt it before any network
            // request. The callback delegates stop -> continuation to the Manager
            // owner; projection then empties the retired request context.
            // Once a later iteration and its gate are both present, projection
            // proceeds from the durable cut.
            match staleRetirement journal sessionId road with
            | Some _ ->
                do! interruptAttempt sessionId
                HostMessageProjection.replaceMessagesInPlace outObj []
                return RelayProjectionDisposition.RetiredAttemptStopped
            | None -> return projectActive road outObj
        }

    let apply
        (journal: AgentJournal option)
        (interruptAttempt: SessionId -> Task<unit>)
        (sessionId: string option)
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
                return! project durable interruptAttempt currentSessionId road outObj
            | None -> return RelayProjectionDisposition.Unchanged
        }
