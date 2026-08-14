namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Persistence.Journal
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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
        if isNull part then
            false
        else
            let kind =
                if isNull part?``type`` then
                    ""
                else
                    unbox<string> part?``type``

            let name =
                if not (isNull part?tool) then unbox<string> part?tool
                elif not (isNull part?name) then unbox<string> part?name
                else ""

            kind = "tool" && name = "chronicle"

    let private blogPartStatus (part: obj) : string option =
        if isNull part || isNull part?state then
            None
        else
            match part?state?status with
            | null -> None
            | value -> Some(unbox<string> value)

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

    /// Any blog tool part on the last assistant (completed/error/pending/running).
    /// Host cleanup after abort marks hanging tools status=error + interrupted=true
    /// and sets assistant time.completed — that is NOT ENFORCER-060 pure prose.
    let hasAnyBlogToolPart (rawMessages: obj list) : bool =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | None -> false
        | Some(_, parts, _) -> parts |> List.exists isBlogToolPart

    let private blogPartInterrupted (part: obj) : bool =
        if isNull part || isNull part?state then
            false
        else
            let meta = part?state?metadata

            if isNull meta then
                false
            else
                match meta?interrupted with
                | null -> false
                | value -> unbox<bool> value = true

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
        if isNull message then
            None
        else
            let info = if isNull message?info then message else message?info

            if
                not (isNull info)
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
    /// Host `createObj` 只写回 id / source / requestKey 侧信道；id 规则保持
    /// `enforcer-repair-` + sha256(requestKey + "|" + RepairInstruction).Substring(0, 24)。
    let withRepairInstruction (rawMessages: obj list) (requestKey: string) : obj list =
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
                                "requestKey", box requestKey ]
                      )
                      "parts", box [| createObj [ "type", box "text"; "text", box repairText ] |] ]

            rawMessages @ [ repairMsg ]
