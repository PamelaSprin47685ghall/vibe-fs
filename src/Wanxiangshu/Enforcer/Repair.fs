namespace Wanxiangshu.Enforcer

open System
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

/// Protocol repair: incomplete/aborted blog tool detection, repair key and
/// repair instruction materialization. Owns only the repair path.
module EnforcerRepair =

    /// Item 15: stable minimal repair instruction (no dynamic context resend).
    let RepairInstruction =
        LlmFacing.renderInstructions
            [ "Protocol repair"
              "Call the chronicle tool exactly once with a non-empty entry. Do not answer in prose." ]

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

    /// ENFORCER-060/061: append the stable InteractionRepair instruction.
    ///
    /// terminal identity makes AABB re-entry idempotent instead of degrading to a
    /// LogicalRun-wide consumed bit. id 规则保持
    /// `enforcer-repair-` + sha256(requestKey + "|" + RepairInstruction).Substring(0, 24)。
    let withRepairInstruction
        (rawMessages: obj list)
        (requestKey: string)
        (repairTerminalRun: ProviderRunIdentity)
        : obj list =
        let msgId =
            "enforcer-repair-"
            + (HostDigest.sha256Hex (requestKey + "|" + RepairInstruction)).Substring(0, 24)

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
                  "parts", box [| createObj [ "type", box "text"; "text", box RepairInstruction ] |] ]

        rawMessages @ [ repairMsg ]
