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

    let private executeCommitted
        (journalOpt: AgentJournal option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        task {
            let objective = args.Text "newScopeObjective"
            let budget = resolveBudget args

            let _oldExperience = args.OptionalText "oldScopeVerboseVerbatimExperience"

            // The durable reanchor must commit before the new budget becomes visible.
            let callId =
                match ctx.ToolCallId with
                | Some id -> id
                | None -> failwith "resetScope requires an exact tool call id in host context (HOST-011)"

            let journal =
                match journalOpt with
                | Some j -> j
                | None ->
                    failwith "resetScope requires a durable agent journal for ScopeReanchored prefix epoch advance"

            let sessionId = SessionId.create ctx.SessionId
            let snapshot = AgentJournal.snapshot journal

            let epoch =
                match AgentProjection.tryFind sessionId snapshot.AgentProjections with
                | None -> PrefixEpochProjection.empty
                | Some session -> session.PrefixEpoch |> Option.defaultValue PrefixEpochProjection.empty

            let nextEpoch = PrefixEpochId.next epoch.EpochId

            let fact =
                ContextFact.ScopeReanchored
                    {| SessionId = sessionId
                       PreviousEpochId = epoch.EpochId
                       NextEpochId = nextEpoch
                       CallId = callId |}

            let! appended = AgentJournal.appendAgent (StreamId.Session sessionId) ctx.ProviderRunId fact journal

            match appended with
            | Ok _ -> ()
            | Error failure -> raise (JournalAppendException failure)

            Surface.resetScopeState ctx.SessionId budget

            // Tool results stay raw; the provider-request view receives its budget suffix.
            return $"Scope reset. Objective: %s{objective}"
        }

    let private execute
        (journalOpt: AgentJournal option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        if Surface.negotiationRequired ctx.SessionId then
            Task.FromResult(
                "resetScope is unavailable until the first negotiate(questions) reply has arrived as a user steer. Ask the delegator clarifying questions now, and continue while the reply is pending."
            )
        else
            executeCommitted journalOpt args ctx

    let spec (factory: HostToolFactory) (journal: AgentJournal option) : ToolSpec =
        { Name = Surface.ResetScopeTool
          Description =
            "Execution barrier and context garbage collector. Removes everything between the previous resetScope call (or session start) and this call from subsequent model requests, except user messages and prior resetScope call/result pairs. Everything after this call belongs to the new scope. Sets turnBudget for the new scope."
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
                  factory ]
          Execute = execute journal }
