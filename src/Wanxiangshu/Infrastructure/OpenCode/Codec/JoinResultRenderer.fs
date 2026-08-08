namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Orchestrator
open Wanxiangshu.Session

/// EXEC-004 rev.2 / docs/how/synthetic-toml.md §9.6: LLM-facing join wire only.
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

    /// Top-level fields, then one blank line, then ordinal-stable result blocks (docs/how/synthetic-toml.md §9.6 layout).
    let private completedDocument (headerFields: string list) (entries: string list) : string =
        String.concat "\n" headerFields + "\n\n" + String.concat "\n" entries + "\n"

    /// EXEC-017: local join interrupt wire. `interrupted` is not
    /// ForkError / failed / aborted. OperatorAbort keeps the existing message;
    /// UserMessageArrived omits user text copy. DeadlineExpired is TIMED_OUT
    /// (renderForkError), not this path.
    let renderInterrupted (reason: JoinInterruptReason) : string =
        match reason with
        | JoinInterruptReason.OperatorAbort ->
            joinBlocks
                [ field "status" (str "interrupted")
                  field "reason" (str "operator_abort")
                  field "message" (str "join interrupted") ]
        | JoinInterruptReason.UserMessageArrived ->
            joinBlocks
                [ field "status" (str "interrupted")
                  field "reason" (str "user_message") ]
        | JoinInterruptReason.DeadlineExpired ->
            // Defensive: production JoinTool maps DeadlineExpired to TIMED_OUT.
            joinBlocks
                [ field "status" (str "interrupted")
                  field "reason" (str "operator_abort")
                  field "message" (str "join interrupted") ]

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

    let private renderAgentJoinItem
        (resolveAgentName: string -> string)
        (ordinal: int)
        (completion: RunCompletion)
        (item: AgentJoinItem)
        : string =
        let name = agentName resolveAgentName completion

        match item with
        | AgentCompletedItem payload ->
            resultEntry ordinal "agent" "completed" [ field "agent" (str name) ] (Some payload.WorkRecord)
        | AgentFailedItem payload ->
            resultEntry
                ordinal
                "agent"
                "failed"
                [ field "agent" (str name)
                  field "code" (str payload.Code)
                  field "message" (str payload.Message) ]
                None
        | AgentAbandonedItem(agentId, reason) ->
            let display =
                if not (String.IsNullOrWhiteSpace name) then
                    name
                else
                    agentId

            resultEntry ordinal "agent" "abandoned" [ field "agent" (str display); field "reason" (str reason) ] None

    let private renderPtyJoinItem (ordinal: int) (item: PtyJoinItem) : string =
        match item with
        | PtyExited payload ->
            resultEntry
                ordinal
                "pty"
                "completed"
                [ field "outcome" (str payload.Outcome)
                  field "closed" (if payload.Closed then "true" else "false")
                  field "pty_id" (str payload.PtyId) ]
                None
        | PtyFailed payload ->
            resultEntry
                ordinal
                "pty"
                "failed"
                [ field "outcome" (str payload.Outcome)
                  field "closed" (if payload.Closed then "true" else "false")
                  field "pty_id" (str payload.PtyId)
                  field "code" (str payload.Code)
                  field "message" (str payload.Message) ]
                None
        | PtyAborted payload ->
            resultEntry
                ordinal
                "pty"
                "aborted"
                [ field "outcome" (str payload.Outcome)
                  field "closed" (if payload.Closed then "true" else "false")
                  field "pty_id" (str payload.PtyId)
                  field "code" (str payload.Code)
                  field "message" (str payload.Message) ]
                None

    let private renderJoinItem (resolveAgentName: string -> string) (ordinal: int) (item: JoinItem) : string =
        match item with
        | AgentItem agentItem ->
            // JoinItem payload carries no AgentName; empty stub forces resolveAgentName(AgentId).
            let nameStub =
                match agentItem with
                | AgentCompletedItem p ->
                    { RunId = p.RunId
                      AgentId = p.AgentId
                      AgentName = ""
                      Role = p.Role
                      Outcome = AgentCompleted p
                      CompletedAt = DateTimeOffset.UtcNow }
                | AgentFailedItem p ->
                    { RunId = p.RunId
                      AgentId = p.AgentId
                      AgentName = ""
                      Role = defaultArg p.Role Role.Executor
                      Outcome = AgentFailed p
                      CompletedAt = DateTimeOffset.UtcNow }
                | AgentAbandonedItem(agentId, reason) ->
                    { RunId = "abandoned-" + agentId
                      AgentId = agentId
                      AgentName = ""
                      Role = Role.Executor
                      Outcome = AgentAbandoned(agentId, reason)
                      CompletedAt = DateTimeOffset.UtcNow }

            renderAgentJoinItem resolveAgentName ordinal nameStub agentItem
        | PtyItem ptyItem -> renderPtyJoinItem ordinal ptyItem

    let private renderCompletionItem
        (isPtyRun: string -> bool)
        (resolveAgentName: string -> string)
        (ordinal: int)
        (completion: RunCompletion)
        : string =
        let item = JoinItem.ofRunCompletion (isPtyRun completion.RunId) completion

        match item with
        | AgentItem agentItem -> renderAgentJoinItem resolveAgentName ordinal completion agentItem
        | PtyItem ptyItem -> renderPtyJoinItem ordinal ptyItem

    /// EXEC-004 / EXEC-018 / EXEC-020: JoinItem batch (production JoinTool path).
    /// PtyAborted → kind=pty,status=aborted without RunCompletion round-trip.
    let renderJoinItemBatch (resolveAgentName: string -> string) (batch: NonEmptyBatch<JoinItem>) : string =
        let items = NonEmptyBatch.toList batch
        let count = List.length items

        let header = [ field "status" (str "completed"); field "count" (string count) ]

        let entries =
            items |> List.mapi (fun i item -> renderJoinItem resolveAgentName (i + 1) item)

        completedDocument header entries

    /// EXEC-004 / EXEC-018: status=completed + count + [[result]] (single item also uses [[result]]).
    /// Compat surface for tests / ofRunCompletion path. Production JoinTool uses renderJoinItemBatch.
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
            | ForkError.JoinInProgress -> "JOIN_IN_PROGRESS", None
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
