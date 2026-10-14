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

    // Stopped. inspect has no active incumbency record and no retirement list.
    // Absence is not a previous tenure and not permission to continue.
    let private dispositionAfterProjection (_work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option) =
        RelayProjectionDisposition.Unchanged

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
    // Stopped. inspect has no relayed retirement and no projection cut.
    let private staleRetirement
        journal
        sessionId
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        acceptedRequest
        messages
        =
        let _ = journal, sessionId, work, acceptedRequest, messages
        None

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

    // Stopped. inspect has no previous retirement range or opening cursor.
    let private previousRange (_work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork) =
        None

    // Stopped. inspect has no incumbency identity for a handover record.
    let private handoverRecord (_work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork) (_text: string) : obj option =
        None

    /// Successor visibility. Opening sequence comes from the active lifecycle
    /// tenure. The other side is the first observation of a captured Host
    /// message id. A message absent from that map has not been captured yet,
    /// so it stays. The retirement cut is not a strip condition.
    let private projectActive
        (journal: AgentJournal)
        (sessionId: SessionId)
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option)
        outObj
        =
        task {
            let messages = ProviderWireDecode.messagesFromTransformOutput outObj

            // Stopped. inspect has no active incumbency record and no retirement list.
            // Absence is not a previous tenure and not permission to continue.
            let _ = journal, sessionId
            HostMessageProjection.replaceMessagesInPlace outObj messages
            return dispositionAfterProjection work
        }

    let private project
        journal
        (interruptAttempt: SessionId -> Task<unit>)
        sessionId
        acceptedRequest
        (lifecycle: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option)
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
        (lifecycle: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option)
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
