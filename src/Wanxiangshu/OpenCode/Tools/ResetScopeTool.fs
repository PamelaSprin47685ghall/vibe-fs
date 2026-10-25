namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Persistence.Journal.AgentJournal
open Wanxiangshu.Context.Scope
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Composition.Durable

module ResetScopeTool =

    let private resolveBudget (args: HostToolArguments) : int =
        match args.OptionalNonNegativeInteger "newScopeTurnBudget" with
        | Ok(Some b) -> max 0 b
        | _ ->
            args.OptionalNumber "newScopeTurnBudget"
            |> Option.map (fun num -> max 0 (int num))
            |> Option.defaultValue 0

    let private executeCommitted (journalOpt: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) : Task<string> =
        task {
            let objective = args.Text "newScopeObjective"
            let budget = Surface.adjustBudgetForKpi (resolveBudget args) (args.Text "kpiCompletion")

            let _oldExperience = args.OptionalText "oldScopeVerboseVerbatimExperience"

            // 1. 获取本会话 sealed 快照，若取不到则明确报错，不提交、不更新内存
            let snapshotOpt = OutboundViewCapture.tryGetSealed ctx.SessionId
            match snapshotOpt with
            | None ->
                return "resetScope failed: no sealed outbound view snapshot captured for this session."
            | Some snapshot ->

            // 2. 序列化为稳定 JSON，写入 journal blob
            let callId =
                match ctx.ToolCallId with
                | Some id -> id
                | None ->
                    failwith "resetScope requires an exact tool call id in host context (HOST-011)"

            let journal =
                match journalOpt with
                | Some j -> j
                | None ->
                    failwith "resetScope requires a durable agent journal for ScopeReanchored prefix epoch advance"

            let archiveJson = OutboundViewCapture.serializeSnapshot snapshot
            let! blobResult = journal.WriteBlob archiveJson
            match blobResult with
            | Error failure ->
                return $"resetScope failed: failed to write archive blob: %s{failure}"
            | Ok receipt ->

            let sessionId = SessionId.create ctx.SessionId
            let journalSnapshot = AgentJournal.snapshot journal
            let epoch =
                match AgentProjection.tryFind sessionId journalSnapshot.AgentProjections with
                | None -> PrefixEpochProjection.empty
                | Some session -> session.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty

            let nextEpoch = PrefixEpochId.next epoch.EpochId
            let fact =
                ContextFact.ScopeReanchored
                    {| SessionId = sessionId
                       PreviousEpochId = epoch.EpochId
                       NextEpochId = nextEpoch
                       CallId = callId
                       ArchiveRef = Some receipt.BlobRef
                       ArchiveDigest = Some receipt.BlobDigest |}

            let! appended = AgentJournal.appendAgent (StreamId.Session sessionId) ctx.ProviderRunId fact journal
            match appended with
            | Ok _ -> ()
            | Error failure -> raise (JournalAppendException failure)

            // 3. append 成功后才更新内存：resetScopeState，并进入 askPreviousScope pending，且标记 hasPreviousScope
            OutboundViewCapture.discardSealed ctx.SessionId
            Surface.resetScopeState ctx.SessionId budget
            if not (Set.isEmpty epoch.ReanchoredScopes) then
                Surface.beginAskPreviousScope ctx.SessionId
            Surface.setHasPreviousScope ctx.SessionId true

            // Tool results stay raw; the provider-request view receives its budget suffix.
            return $"Scope reset. Objective: %s{objective}"
        }

    let private resetAlreadyCommitted (journalOpt: AgentJournal option) (ctx: HostToolContext) =
        match journalOpt, ctx.ToolCallId with
        | Some journal, Some callId ->
            let state = AgentJournal.snapshot journal
            AgentProjection.tryFind (SessionId.create ctx.SessionId) state.AgentProjections
            |> Option.bind (fun session -> session.PrefixEpoch)
            |> Option.exists (PrefixEpochProjection.isScopeReanchored (ToolCallId.value callId))
        | _ -> false

    let private execute (journalOpt: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) : Task<string> =
        if resetAlreadyCommitted journalOpt ctx then
            Task.FromResult("Scope reset. Objective: " + args.Text "newScopeObjective")
        elif Surface.negotiationRequired ctx.SessionId then
            Task.FromResult("resetScope is unavailable until the first negotiate(questions) reply has arrived as a user steer. Ask the delegator clarifying questions now, and continue while the reply is pending.")
        elif Surface.isPreviousScopePending ctx.SessionId then
            Task.FromResult("resetScope is unavailable until askPreviousScope(questions) has been called to clarify questions with the previous scope. Call askPreviousScope now; ordinary tools remain available.")
        else
            executeCommitted journalOpt args ctx

    let spec (factory: HostToolFactory) (journal: AgentJournal option) : ToolSpec =
        { Name = Surface.ResetScopeTool
          Description =
            "Execution barrier and context garbage collector. Preserves operator messages and the complete historical call/result pairs for resetScope, assume, negotiate, askPreviousScope, and advisor across later resets. Other old-scope working context is removed from subsequent model requests. Requires a KPI completion level; the Host scales the proposed new scope turnBudget using that tier."
          Arguments =
            [ "oldScopeVerboseVerbatimExperience",
              ToolHostCodec.optionalStringSchemaDescribed
                  "Write out your experience and findings from the closing scope, verbosely: everything between the previous resetScope call (or session start) and this call. Any exact text needed in the next scope (such as function signatures, error stack traces, or exact line numbers) MUST be copied VERBATIM here. Null or omitted if there is no prior work."
                  factory
              "newScopeObjective",
              ToolHostCodec.stringSchemaDescribed
                  "The objective for the new scope. The scope must form a self-contained state closed-loop (状态闭环)."
                  factory
              "newScopeTurnBudget",
              ToolHostCodec.nonNegativeIntegerSchemaDescribed
                  "Estimated number of LLM turns needed for the new scope (1 LLM request = 1 turn)."
                  factory
              "kpiCompletion",
              ToolHostCodec.enumSchemaDescribed
                  [ "not_started"; "far_below"; "partially_met"; "met"; "exceeded"; "not_applicable" ]
                  "Report the previous scope KPI achievement: not_started (0.50x), far_below (0.75x), partially_met (0.90x), met (1.00x), exceeded (1.25x), or not_applicable for first scope (1.00x). The host applies the factor to newScopeTurnBudget and rounds upward."
                  factory ]
          Execute = execute journal }
