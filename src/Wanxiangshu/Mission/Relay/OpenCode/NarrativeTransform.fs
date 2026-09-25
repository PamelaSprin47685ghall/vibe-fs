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

    let private cutToolIndex (cut: ProjectionCut) messages =
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

    let private requestBelongsToSuccessor (physical: string option) afterCut freshRoot acceptedHuman gatePhysical =
        match physical with
        | Some current ->
            freshRoot = Some current
            || gatePhysical = Some current
            || (afterCut && acceptedHuman)
        | _ -> false

    let private isSuccessorRequest
        journal
        sessionId
        (road: RoadView)
        (retirement: RetirementSummary)
        acceptedHuman
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

        requestBelongsToSuccessor physical afterCut freshRoot acceptedHuman gatePhysical

    let private staleRetirement journal sessionId (road: RoadView) acceptedHuman messages =
        road.LatestRetirement
        |> Option.filter (fun retirement ->
            not (isSuccessorRequest journal sessionId road retirement acceptedHuman messages))

    // The provider view keeps the full physical history after a retirement:
    // the next iteration sees every prior message, the retirement tool call
    // and its own fresh-head prompt. The cut now only decides request
    // identity for stale attempts; it never filters the provider message set.
    let private projectActive (road: RoadView) outObj =
        let messages = ProviderWireDecode.messagesFromTransformOutput outObj

        HostMessageProjection.replaceMessagesInPlace outObj messages
        dispositionAfterProjection road

    let private project journal (interruptAttempt: SessionId -> Task<unit>) sessionId road acceptedHuman outObj =
        task {
            let messages = ProviderWireDecode.messagesFromTransformOutput outObj

            // An active LogicalRun or a claimed loop gate does not identify this
            // physical request: both can coexist with the retired attempt.
            match staleRetirement journal sessionId road acceptedHuman messages with
            | Some _ ->
                do! interruptAttempt sessionId
                HostMessageProjection.replaceMessagesInPlace outObj []
                return RelayProjectionDisposition.RetiredAttemptStopped
            | None -> return projectActive road outObj
        }

    let apply
        (journal: AgentJournal option)
        (acceptedHuman: bool)
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
                return! project durable interruptAttempt currentSessionId road acceptedHuman outObj
            | None -> return RelayProjectionDisposition.Unchanged
        }
