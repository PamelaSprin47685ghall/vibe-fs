namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Change
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider

/// EXEC-004 / EXEC-017 / EXEC-030: LLM-facing join wire — natural language + delivered view only.
/// No status / count / ordinal / kind / agent / code / message DTO plane.
///
/// Directional delivered-view plane (delegation-013 / delegation-019 / provider-projection-009):
/// child → parent join MUST keep the delivered view in Instruction Plane. Do NOT
/// wrap it in `view = …` or any other TOML data field. Parent → child fork payload is the opposite contract
/// (`commissioner_record` / `attached_view` fields) — do not conflate.
module JoinResultRenderer =

    [<RequireQualifiedAccess>]
    module Path =

        [<Literal>]
        val AgentFailed: string = "tool/join/agent-failed"

        [<Literal>]
        val AgentVoided: string = "tool/join/agent-voided"

        [<Literal>]
        val TerminalLabel: string = "tool/join/terminal-label"

        [<Literal>]
        val OrchestratorPublished: string = "tool/join/orchestrator-published"

    /// EXEC-017: interrupt consequences are natural language, not error DTO.
    val renderInterrupted: lang: ProviderLanguage -> reason: JoinInterruptReason -> string

    /// EXEC-004 / EXEC-018 / EXEC-020: JoinItem batch (production JoinTool path).
    val renderJoinItemBatch:
        lang: ProviderLanguage ->
        resolveAgentName: (string -> string) ->
        batch: NonEmptyBatch<JoinItem> ->
        resolveTerminalLabel: (string -> string) ->
            string

    /// FIFO window-bounded render. Returns rendered text and un-rendered remaining items.
    val renderJoinItemBatchWithWindow:
        lang: ProviderLanguage ->
        resolveAgentName: (string -> string) ->
        batch: NonEmptyBatch<JoinItem> ->
        resolveTerminalLabel: (string -> string) ->
            string * JoinItem list

    /// EXEC-019: orchestrator verdict batch (FIFO; caller already capped at MaxJoinBatch).
    val renderOrchestratorBatch: lang: ProviderLanguage -> verdicts: NonEmptyBatch<OrchestratorVerdict> -> string

    /// True ForkError path — natural language only (not user interrupt).
    val renderForkError: lang: ProviderLanguage -> error: ForkError -> resolveAgentName: (string -> string) -> string
