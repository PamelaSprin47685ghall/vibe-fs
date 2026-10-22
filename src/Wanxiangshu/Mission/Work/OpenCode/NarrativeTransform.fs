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
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Mission.Planning
open Wanxiangshu.Persistence.Journal

module RelayNarrativeTransform =
    let private messageId message =
        ProviderWireDecode.hostMessageId message

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    /// The Host's message role, passed to the projection library verbatim.
    let private messageRole message : obj =
        let info = readField (readField message "info") "role"

        if isNull info then readField message "role" else info

    /// Every cut candidate this message carries.
    let private toolCallIds message : string list =
        ProviderWireDecode.rawPartsOf message
        |> List.choose ProviderWireDecode.decodePart
        |> List.choose (function
            | WireToolCall(toolCallId, _, _) -> Some(ToolCallId.value toolCallId)
            | WireToolResult(toolCallId, _) -> Some(ToolCallId.value toolCallId)
            | _ -> None)

    let private facts (messages: obj list) : RelayMessageFact list =
        messages
        |> List.map (fun message ->
            { Id = messageId message
              ToolCallIds = toolCallIds message })

    /// 退休 attempt 判定的唯一实现：wanxiangli/projection `retired`（CUT-17）。
    [<AllowNullLiteral>]
    type private RetiredRule =
        abstract retired: request: obj -> bool

    [<Import("Projection", "wanxiangli/projection")>]
    let private createProjection: unit -> RetiredRule = jsNative

    let private projection = createProjection ()

    /// The library request. The neutral core decided which message ids, cuts and
    /// accepted roots the rule sees; this only adds the pass-through role.
    let private libraryRequest (roles: obj list) (request: RelayRetirementRequest) : obj =
        let messages =
            List.map2
                (fun role (message: RelayMessagePolicy) ->
                    createObj
                        [ "role" ==> role
                          "id" ==> Option.toObj message.Id
                          "cut" ==> message.Cut
                          "accepted" ==> Option.toObj message.Accepted ])
                roles
                request.Messages
            |> List.toArray

        createObj
            [ "relayed" ==> true
              "acceptedRequest" ==> request.AcceptedRequest
              "activeRoot" ==> Option.toObj request.ActiveRoot
              "gatePhysical" ==> Option.toObj request.GatePhysical
              "messages" ==> messages ]

    let private staleRetirement
        (journal: AgentJournal)
        (sessionId: SessionId)
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        (acceptedRequest: bool)
        (roles: obj list)
        (messages: obj list)
        =
        RelayProjection.staleRequest journal sessionId work acceptedRequest (facts messages)
        |> Option.filter (fun request -> projection.retired (libraryRequest roles request))

    let isStaleRetiredAttempt
        (journal: AgentJournal option)
        (acceptedRequest: bool)
        (sessionId: string option)
        (lifecycle: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option)
        (outObj: obj)
        : bool =
        match journal, sessionId, lifecycle with
        | Some durable, Some value, Some work when not (String.IsNullOrWhiteSpace value) ->
            let messages = ProviderWireDecode.messagesFromTransformOutput outObj

            staleRetirement
                durable
                (SessionId.create value)
                work
                acceptedRequest
                (messages |> List.map messageRole)
                messages
            |> Option.isSome
        | _ -> false

    /// 条款 4：本次请求不再放行时清空消息——请求因此没有内容，LLM 看不到
    /// deliver 回复。退休 attempt 与交付裁决共用这一个动作。
    let clearRequestMessages (outObj: obj) =
        HostMessageProjection.replaceMessagesInPlace outObj []

    /// S1/S2/S3/S4 relay no longer clears context. Opening cursor records the
    /// tenure boundary; it does not strip predecessor assistant or tool
    /// messages. Retention and truncation belong to the view declaration.
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
                |> Option.bind (fun work ->
                    staleRetirement journal sessionId work acceptedRequest (messages |> List.map messageRole) messages)

            match stale with
            | Some _ ->
                do! interruptAttempt sessionId
                clearRequestMessages outObj
                return RelayProjectionDisposition.RetiredAttemptStopped
            | None -> return RelayProjection.disposition lifecycle
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
                return! project durable interruptAttempt (SessionId.create value) acceptedRequest (Some work) outObj
            | _ -> return RelayProjectionDisposition.Unchanged
        }

    let RelayNarrativeTransform_apply
        (journal: AgentJournal option)
        (acceptedRequest: bool)
        (interruptAttempt: SessionId -> Task<unit>)
        (sessionId: string option)
        (lifecycle: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option)
        (outObj: obj)
        : Task<RelayProjectionDisposition> =
        apply journal acceptedRequest interruptAttempt sessionId lifecycle outObj
