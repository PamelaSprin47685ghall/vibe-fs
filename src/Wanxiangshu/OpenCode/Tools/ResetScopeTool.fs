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

    let private execute (journalOpt: AgentJournal option) (args: HostToolArguments) (ctx: HostToolContext) : Task<string> =
        task {
            let objective = args.Text "newScopeObjective"
            let budget = resolveBudget args

            let _oldExperience = args.OptionalText "oldScopeVerboseVerbatimExperience"

            // 1. Update in-memory session state
            Surface.resetScopeState ctx.SessionId budget

            // 2. Append durable ScopeReanchored fact if journal is present
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

            // 3. Return tool result carrying the new budget suffix
            let responseText = $"Scope reset. Objective: %s{objective}"
            let suffixed = Protocol.appendText (box responseText) (Protocol.budgetSuffix budget)
            return unbox<string> suffixed
        }

    let spec (factory: HostToolFactory) (journal: AgentJournal option) : ToolSpec =
        { Name = Protocol.ResetScopeTool
          Description =
            "Execution barrier and context garbage collector. Deletes everything between the previous resetScope call (or session start) and this call, keeping only user messages and prior resetScope pairs; everything after this call is the new scope. Also sets the turnBudget for the new scope."
          Arguments =
            [ "oldScopeVerboseVerbatimExperience",
              ToolHostCodec.optionalStringSchemaDescribed
                  "Verbose experience and findings from the closing scope: everything between the previous resetScope call (or session start) and this call. Null/omitted if there is no prior work. If any exact text (function signatures, error stack traces, exact line numbers) is needed in the next scope, it MUST be copied VERBATIM here."
                  factory
              "newScopeObjective",
              ToolHostCodec.stringSchemaDescribed
                  "The state closed-loop (状态闭环) objective for the new scope."
                  factory
              "newScopeTurnBudget",
              ToolHostCodec.nonNegativeIntegerSchemaDescribed
                  "Estimated LLM turns (1 LLM request = 1 turn) for the new scope."
                  factory ]
          Execute = execute journal }
