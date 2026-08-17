namespace Wanxiangshu.Enforcer

open Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Protocol repair: incomplete/aborted blog tool detection, repair key and
/// repair instruction projection. Owns only the repair path.
module EnforcerRepair =

    /// Item 15: stable minimal repair instruction (no dynamic context resend).
    /// Domain 单源 — `ProjectionConstants.RepairInstruction`（PROJ-008 Step4）。
    let RepairInstruction = ProjectionConstants.RepairInstruction

    let tryOpenByBlogger
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        : OpenBloggerRequest option =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind mainSessionId
        |> Option.bind (fun session -> session.BloggerCycles)
        |> Option.bind (fun cycles -> BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles)

    let private isBlogToolPart (part: obj) : bool =
        let kind =
            if isNull part || isNull part?``type`` then
                ""
            else
                unbox<string> part?``type``

        let name =
            if not (isNull part) && not (isNull part?tool) then
                unbox<string> part?tool
            elif not (isNull part) && not (isNull part?name) then
                unbox<string> part?name
            else
                ""

        kind = "tool" && name = "chronicle"

    let chronicleCallCount (rawMessages: obj list) : int =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | None -> 0
        | Some(_, parts, _) -> parts |> List.filter isBlogToolPart |> List.length

    let private blogPartStatus (part: obj) : string option =
        if isNull part || isNull part?state || isNull part?state?status then
            None
        else
            Some(unbox<string> part?state?status)

    /// pending/running blog: Host will re-enter after tool completion — not pure prose.
    let hasIncompleteBlogTool (rawMessages: obj list) : bool =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) ->
            parts
            |> List.exists (fun part ->
                isBlogToolPart part
                && match blogPartStatus part with
                   | Some "pending"
                   | Some "running" -> true
                   | _ -> false)

    let hasCompletedBlogTool (rawMessages: obj list) : bool =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) ->
            parts
            |> List.exists (fun part -> isBlogToolPart part && blogPartStatus part = Some "completed")

    /// Any blog tool part on the last assistant (completed/error/pending/running/statusless).
    /// Host cleanup after abort marks hanging tools status=error + interrupted=true
    /// and sets assistant time.completed — that is NOT ENFORCER-060 pure prose.
    let hasAnyBlogToolPart (rawMessages: obj list) : bool =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) -> parts |> List.exists isBlogToolPart

    let private blogPartInterrupted (part: obj) : bool =
        if
            isNull part
            || isNull part?state
            || isNull part?state?metadata
            || isNull part?state?metadata?interrupted
        then
            false
        else
            unbox<bool> part?state?metadata?interrupted = true

    /// Host abort/cleanup terminal: `SessionProcessor.cleanup` marks every hanging
    /// tool `status=error` + `metadata.interrupted=true`
    /// (`../opencode/packages/opencode/src/session/processor.ts:589`). That is the
    /// owner turn being killed, not the Blogger producing a bad cycle, so LOOP-006
    /// forbids it from spending the primary A/A/B/B budget.
    let hasAbortedBlogAttempt (rawMessages: obj list) : bool =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) ->
            parts
            |> List.exists (fun part ->
                isBlogToolPart part
                && match blogPartStatus part with
                   | Some "completed" -> false
                   | Some "pending"
                   | Some "running" -> false
                   | _ -> blogPartInterrupted part)

    /// ENFORCER-065 `ToolExecutionError`: the blog call itself failed without an
    /// abort — a real invalid cycle, which skips the nudge and goes to Fallback.
    let hasErroredBlogAttempt (rawMessages: obj list) : bool =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) ->
            parts
            |> List.exists (fun part ->
                isBlogToolPart part
                && not (blogPartInterrupted part)
                && match blogPartStatus part with
                   | Some "error" -> true
                   | _ -> false)

    /// Extract the requestKey from an interaction-repair synthetic user message.
    let repairRequestKey (message: obj) : string option =
        let info =
            if isNull message then null
            elif isNull message?info then message
            else message?info

        if
            not (isNull message)
            && not (isNull info)
            && not (isNull info?source)
            && unbox<string> info?source = "interaction-repair"
            && not (isNull info?synthetic)
            && unbox<bool> info?synthetic
            && not (isNull info?requestKey)
        then
            Some(unbox<string> info?requestKey)
        else
            None

    /// ENFORCER-060/061: InteractionRepair via Projection algebra (PROJ-008 Step4).
    ///
    /// 消息正文 / 顺序来自 `InsertRepair` → plan → renderMessagesWithIntents。
    /// Host `createObj` 只写回 id / source / requestKey / repairTerminalRun 侧信道；
    /// terminal identity makes AABB re-entry idempotent instead of degrading to a
    /// LogicalRun-wide consumed bit. id 规则保持
    /// `enforcer-repair-` + sha256(requestKey + "|" + RepairInstruction).Substring(0, 24)。
    let withRepairInstruction
        (rawMessages: obj list)
        (requestKey: string)
        (repairTerminalRun: ProviderRunIdentity)
        : obj list =
        let baseWire = rawMessages |> List.choose ProviderWireCapture.decodeMessage

        let emptyCurrent: ProviderProjection.ProviderSemanticProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = [] }

        let snapshot: ProjectionSnapshot =
            { CurrentProjection = emptyCurrent
              CommittedPrefix = None
              BlogFrames = []
              TransportMessages = Set.empty
              HostReanchor = None }

        let intents = [ ProjectionIntent.InsertRepair { RequestKey = requestKey } ]

        match ProjectionPlanner.plan intents with
        | Error _ ->
            // fail-closed: 不注入手写 list；返回未改 raw（调用方仍 project 原视图）
            rawMessages
        | Ok ordered ->
            let wire = ProjectionRenderer.renderMessagesWithIntents snapshot baseWire ordered

            let msgId =
                "enforcer-repair-"
                + (HostDigest.sha256Hex (requestKey + "|" + ProjectionConstants.RepairInstruction))
                    .Substring(0, 24)

            // InsertRepair 只追加：前缀保留原始 raw（含 id）；尾部正文来自 algebra。
            let repairText =
                wire
                |> List.tryLast
                |> Option.bind (fun msg ->
                    msg.Parts
                    |> List.tryPick (function
                        | ProviderProjection.WireText t -> Some t
                        | _ -> None))
                |> Option.defaultValue ProjectionConstants.RepairInstruction

            let repairMsg =
                createObj
                    [ "info",
                      box (
                          createObj
                              [ "id", box msgId
                                "role", box "user"
                                "synthetic", box true
                                "source", box "interaction-repair"
                                "requestKey", box requestKey
                                "repairTerminalRun", box (ProviderRunIdentity.value repairTerminalRun) ]
                      )
                      "parts", box [| createObj [ "type", box "text"; "text", box repairText ] |] ]

            rawMessages @ [ repairMsg ]
