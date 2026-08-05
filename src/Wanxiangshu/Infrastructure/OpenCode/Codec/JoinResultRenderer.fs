namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Orchestrator
open Wanxiangshu.Session

/// EXEC-004 rev.2 / spec/13 §9.6: LLM-facing join wire only.
/// work_record is entry-local comment (SyntheticToml.comment), never a TOML field.
/// Internal journal / HandleCompletionCodec work_record is unchanged.
///
/// Does not use SyntheticToml.document for batches: document partitions bare vs table by
/// first line, so a comment-prefixed [[result]] would be reordered relative to plain
/// [[result]] (pty / failed items). Assembly is ordinal-stable concatenation.
module JoinResultRenderer =

    let private field name value = SyntheticToml.field name value
    let private str value = SyntheticToml.renderString value

    let private joinBlocks (blocks: string list) : string =
        (String.concat "\n" (blocks |> List.filter (fun s -> s <> ""))) + "\n"

    /// Top-level fields, then one blank line, then ordinal-stable result blocks (spec/13 §9.6 layout).
    let private completedDocument (headerFields: string list) (entries: string list) : string =
        String.concat "\n" headerFields + "\n\n" + String.concat "\n" entries + "\n"

    /// EXEC-017: interrupt is not ForkError / failed / aborted.
    let renderInterrupted () : string =
        joinBlocks
            [ field "status" (str "interrupted")
              field "reason" (str "new_user_message")
              field "action" (str "handle_latest_user_message") ]

    /// One [[result]] block; optional leading LWR comment lines (agent completed only).
    let private resultEntry
        (ordinal: int)
        (kind: string)
        (status: string)
        (extraFields: string list)
        (workRecordComment: string option)
        : string =
        let fields =
            [ field "ordinal" (string ordinal)
              field "kind" (str kind)
              field "status" (str status) ]
            @ extraFields

        let table = SyntheticToml.tableArrayEntry "result" fields

        match workRecordComment with
        | Some wr when not (String.IsNullOrEmpty wr) -> SyntheticToml.comment wr + "\n" + table
        | _ -> table

    /// resolveAgentName: empty string = unknown agent; non-empty is ManagedAgent raw or name.
    let private agentName (resolveAgentName: string -> string) (completion: RunCompletion) : string =
        if not (String.IsNullOrWhiteSpace completion.AgentName) then
            match ManagedAgent.tryParse completion.AgentName with
            | Some agent -> agent.Name
            | None -> completion.AgentName
        else
            let raw = resolveAgentName completion.AgentId

            if String.IsNullOrWhiteSpace raw then
                completion.AgentId
            else
                match ManagedAgent.tryParse raw with
                | Some agent -> agent.Name
                | None -> raw

    let private renderAgentItem
        (resolveAgentName: string -> string)
        (ordinal: int)
        (completion: RunCompletion)
        : string =
        let name = agentName resolveAgentName completion

        match completion.Outcome with
        | AgentCompleted payload ->
            resultEntry ordinal "agent" "completed" [ field "agent" (str name) ] (Some payload.WorkRecord)
        | AgentFailed payload ->
            resultEntry
                ordinal
                "agent"
                "failed"
                [ field "agent" (str name)
                  field "code" (str payload.Code)
                  field "message" (str payload.Message) ]
                None
        | AgentAborted payload ->
            resultEntry
                ordinal
                "agent"
                "aborted"
                [ field "agent" (str name)
                  field "code" (str payload.Code)
                  field "message" (str payload.Message) ]
                None
        | AgentAbandoned(agentId, reason) ->
            let display =
                if not (String.IsNullOrWhiteSpace name) then
                    name
                else
                    agentId

            resultEntry ordinal "agent" "abandoned" [ field "agent" (str display); field "reason" (str reason) ] None

    let private renderPtyItem (ordinal: int) (completion: RunCompletion) : string =
        match completion.Outcome with
        | AgentCompleted payload ->
            resultEntry
                ordinal
                "pty"
                "completed"
                [ field "outcome" (str payload.WorkRecord)
                  field "closed" "true"
                  field "pty_id" (str completion.RunId) ]
                None
        | AgentFailed payload
        | AgentAborted payload ->
            let status =
                match completion.Outcome with
                | AgentAborted _ -> "aborted"
                | _ -> "failed"

            resultEntry
                ordinal
                "pty"
                status
                [ field "outcome" (str payload.Message)
                  field "closed" "true"
                  field "pty_id" (str completion.RunId)
                  field "code" (str payload.Code)
                  field "message" (str payload.Message) ]
                None
        | AgentAbandoned(_, reason) ->
            resultEntry
                ordinal
                "pty"
                "abandoned"
                [ field "outcome" (str reason)
                  field "closed" "true"
                  field "pty_id" (str completion.RunId)
                  field "reason" (str reason) ]
                None

    let private renderCompletionItem
        (isPtyRun: string -> bool)
        (resolveAgentName: string -> string)
        (ordinal: int)
        (completion: RunCompletion)
        : string =
        if isPtyRun completion.RunId then
            renderPtyItem ordinal completion
        else
            renderAgentItem resolveAgentName ordinal completion

    /// EXEC-004 / EXEC-018: status=completed + count + [[result]] (single item also uses [[result]]).
    /// Pure: no HostForkRuntime — caller supplies isPtyRun / resolveAgentName (empty = unknown).
    let renderCompletedBatch
        (isPtyRun: string -> bool)
        (resolveAgentName: string -> string)
        (batch: NonEmptyBatch<RunCompletion>)
        : string =
        let items = NonEmptyBatch.toList batch
        let count = List.length items

        let header = [ field "status" (str "completed"); field "count" (string count) ]

        let entries =
            items
            |> List.mapi (fun i c -> renderCompletionItem isPtyRun resolveAgentName (i + 1) c)

        completedDocument header entries

    /// EXEC-019: orchestrator verdict batch (FIFO; caller already capped at MaxJoinBatch).
    let renderOrchestratorBatch (verdicts: NonEmptyBatch<OrchestratorVerdict>) : string =
        let items = NonEmptyBatch.toList verdicts
        let count = List.length items

        let header = [ field "status" (str "completed"); field "count" (string count) ]

        let entries =
            items
            |> List.mapi (fun i verdict ->
                resultEntry (i + 1) "orchestrator" "completed" [ field "outcome" (str (sprintf "%A" verdict)) ] None)

        completedDocument header entries

    /// True ForkError path (NothingToJoin / Cancelled / …) — not user interrupt.
    /// EXEC-009 Abandoned is a [[result]] batch item (renderCompletedBatch), not
    /// a top-level failed withhold. ForkError.Abandoned remains for legacy callers;
    /// wire still includes agent so the failure surface names the handle.
    let renderForkError (error: ForkError) : string =
        let code, agentOpt =
            match error with
            | ForkError.NothingToJoin -> "NOTHING_TO_JOIN", None
            | ForkError.Cancelled -> "CANCELLED", None
            | ForkError.Empty -> "EMPTY", None
            | ForkError.Abandoned(id, reason) -> "ABANDONED:" + id + ":" + reason, Some id
            | ForkError.NotFound id -> "NOT_FOUND:" + id, Some id
            | ForkError.TimedOut -> "TIMED_OUT", None
            | ForkError.TerminalMaterializationFailed id -> "TERMINAL_MATERIALIZATION_FAILED:" + id, Some id

        let header =
            match agentOpt with
            | Some agentId ->
                [ field "status" (str "failed")
                  field "agent" (str agentId)
                  "[error]"
                  field "code" (str code)
                  field "message" (str (error.ToString())) ]
            | None ->
                [ field "status" (str "failed")
                  "[error]"
                  field "code" (str code)
                  field "message" (str (error.ToString())) ]

        joinBlocks header
