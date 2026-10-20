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
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.Mission.Planning
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type RelayProjectionDisposition =
    | Unchanged
    | CurrentTenure
    | RetiredAttemptStopped

module RelayNarrativeTransform =
    let private messageId message =
        ProviderWireDecode.hostMessageId message

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    /// 退休 attempt 判定的唯一实现：wanxiangli/projection `retired`（CUT-17）。
    [<AllowNullLiteral>]
    type private RetiredRule =
        abstract retired: request: obj -> bool

    [<Import("Projection", "wanxiangli/projection")>]
    let private createProjection: unit -> RetiredRule = jsNative

    let private projection = createProjection ()

    /// 宿主消息 id 从不为空白（`ProviderWireDecode.hostMessageId`），所以空白的根/物理 id 永远匹配不上；
    /// 交库前记为 null，库只收非空 id。
    let private nonBlank (value: string option) =
        value |> Option.filter (String.IsNullOrWhiteSpace >> not) |> Option.toObj

    let private messageRole message : obj =
        let info = readField (readField message "info") "role"

        if isNull info then readField message "role" else info

    let private carriesCut (cutToolCallId: string) message =
        ProviderWireDecode.rawPartsOf message
        |> List.choose ProviderWireDecode.decodePart
        |> List.exists (function
            | WireToolCall(toolCallId, _, _)
            | WireToolResult(toolCallId, _) -> ToolCallId.value toolCallId = cutToolCallId
            | _ -> false)

    /// The projection disposition of one request. An active tenure means the
    /// message set belongs to the current iteration; a work with no active
    /// tenure has no iteration to project and stays unchanged. Absence is not
    /// a previous tenure and not permission to continue.
    let private dispositionAfterProjection (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option) =
        match work with
        | Some current when current.ActiveId.IsSome -> RelayProjectionDisposition.CurrentTenure
        | _ -> RelayProjectionDisposition.Unchanged

    /// The retired attempt is decided from the unified lifecycle work: an S4
    /// work whose current tenure was Relayed, with a durable retirement cut.
    /// The request is stale unless it belongs to the successor (the loop-gate
    /// nudge, or an accepted request observed after the cut).
    let private staleRetirement
        (journal: AgentJournal)
        (sessionId: SessionId)
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        (acceptedRequest: bool)
        (messages: obj list)
        =
        work.Retirements
        |> List.tryFind (fun retirement -> retirement.ProjectionCut && retirement.Outcome = "Relayed")
        |> Option.filter (fun retirement ->
            let projections = (AgentJournal.snapshot journal).AgentProjections
            let sessionProjection = AgentProjection.tryFind sessionId projections

            let authority =
                sessionProjection |> Option.bind (fun session -> session.PromptAuthority)

            let activeRoot =
                authority
                |> Option.bind (fun value -> value.ActiveLogicalRun)
                |> Option.map (fun profile -> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)

            let gatePhysical =
                authority
                |> Option.bind (fun value ->
                    PromptAuthority.gateNudgeAcceptedPhysical
                        sessionId
                        PromptAuthority.ContinuationKind.ManagerGuard
                        (ManagerLoopGate.gateKind (RetirementId.create retirement.RetirementId))
                        (ProviderRunIdentity.create retirement.Fence.ProviderRunId)
                        value)
                |> Option.map PhysicalUserMessageId.value

            let accepted (id: string option) =
                id
                |> Option.bind (fun physicalId ->
                    ChatExecutionProjection.byKey
                        { ChatExecutionKey.SessionId = sessionId
                          PhysicalUserMessageId = PhysicalUserMessageId.create physicalId }
                        projections.ChatExecutions)
                |> Option.map (fun execution ->
                    AuthorityRootUserMessageId.value execution.acceptedEvidence.AuthorityRootUserMessageId)

            let cutOf =
                match work.ProjectionCut with
                | Some cut -> carriesCut cut.ToolCallId
                | None -> fun _ -> false

            let messageOf message =
                let id = messageId message

                createObj
                    [ "role" ==> messageRole message
                      "id" ==> Option.toObj id
                      "cut" ==> cutOf message
                      "accepted" ==> nonBlank (accepted id) ]

            projection.retired (
                createObj
                    [ "relayed" ==> true
                      "acceptedRequest" ==> acceptedRequest
                      "activeRoot" ==> nonBlank activeRoot
                      "gatePhysical" ==> nonBlank gatePhysical
                      "messages" ==> (messages |> List.map messageOf |> List.toArray) ]
            ))

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

            staleRetirement durable (SessionId.create value) work acceptedRequest messages
            |> Option.isSome
        | _ -> false

    /// S1/S2/S3/S4 relay no longer clears context. Opening cursor records the
    /// tenure boundary; it does not strip predecessor assistant or tool
    /// messages. Retention and truncation belong to the view declaration.
    let private projectActive
        (_journal: AgentJournal)
        (_sessionId: SessionId)
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option)
        _outObj
        =
        task { return dispositionAfterProjection work }

    /// 条款 4：本次请求不再放行时清空消息——请求因此没有内容，LLM 看不到
    /// deliver 回复。退休 attempt 与交付裁决共用这一个动作。
    let clearRequestMessages (outObj: obj) =
        HostMessageProjection.replaceMessagesInPlace outObj []

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
                clearRequestMessages outObj
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
                return! project durable interruptAttempt (SessionId.create value) acceptedRequest (Some work) outObj
            | _ -> return RelayProjectionDisposition.Unchanged
        }
