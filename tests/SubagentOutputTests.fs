module Wanxiangshu.Tests.SubagentOutputTests

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TranscriptDecision
open Wanxiangshu.Tests.Assert

let private fail (msg: string) = check msg false

// ── classifyTurnEvidence: TodosCompleted must not discard assistant text ──

/// When the coder subagent completes all todos AND produces a final text
/// summary, the output must be the assistant text, not "".
/// This is the root cause of coder subagent always showing "(no output)".
let todosCompletedWithAssistantTextReturnsText () =
    let evidence =
        { CurrentTurnEvidence.empty with
            Todos = TodosCompleted
            Assistant = AssistantSnapshot("", 0L, "Done: fixed the bug", Some NormalFinish) }

    match classifyTurnEvidence evidence with
    | CompleteNaturally output ->
        check "output should be assistant text, not empty" (output <> "")
        equal "output is assistant text" "Done: fixed the bug" output
    | other -> fail ("expected CompleteNaturally, got " + string other)

/// When todos are completed but there is NO assistant text, output is "".
/// This is the legitimate fallback.
let todosCompletedWithoutAssistantTextReturnsEmpty () =
    let evidence =
        { CurrentTurnEvidence.empty with
            Todos = TodosCompleted
            Assistant = NoAssistant }

    match classifyTurnEvidence evidence with
    | CompleteNaturally output -> equal "empty output when no assistant text" "" output
    | other -> fail ("expected CompleteNaturally, got " + string other)

/// When todos are completed with EmptyAssistant, output is "".
let todosCompletedWithEmptyAssistantReturnsEmpty () =
    let evidence =
        { CurrentTurnEvidence.empty with
            Todos = TodosCompleted
            Assistant = EmptyAssistant }

    match classifyTurnEvidence evidence with
    | CompleteNaturally output -> equal "empty output when EmptyAssistant" "" output
    | other -> fail ("expected CompleteNaturally, got " + string other)

// ── mergeAssistant: refresh evidence must not overwrite good text with empty ──

/// When refreshChildTurnEvidence builds an AssistantSnapshot with empty text
/// (because the last assistant message had only tool-call parts), merging it
/// into existing streaming evidence (which has real text) must NOT destroy the
/// real text.
let mergeAssistantSnapshotEmptyDoesNotOverwriteRealText () =
    let streaming = AssistantSnapshot("", 0L, "real summary text", Some NormalFinish)
    let refresh = AssistantSnapshot("", 0L, "", Some ToolFinish)

    let merged =
        CurrentTurnEvidence.merge
            { CurrentTurnEvidence.empty with
                Assistant = streaming }
            { CurrentTurnEvidence.empty with
                Assistant = refresh }

    match merged.Assistant with
    | AssistantSnapshot(_, _, text, _) -> equal "merge preserves non-empty streaming text" "real summary text" text
    | other -> fail ("expected AssistantSnapshot with text, got " + string other)

/// Normal merge: refresh with non-empty text overwrites streaming (refresh is
/// more authoritative when it has data).
let mergeAssistantSnapshotNonEmptyOverwritesStreaming () =
    let streaming = AssistantSnapshot("", 0L, "partial text", Some NormalFinish)
    let refresh = AssistantSnapshot("", 0L, "full final text", Some NormalFinish)

    let merged =
        CurrentTurnEvidence.merge
            { CurrentTurnEvidence.empty with
                Assistant = streaming }
            { CurrentTurnEvidence.empty with
                Assistant = refresh }

    match merged.Assistant with
    | AssistantSnapshot(_, _, text, _) -> equal "refresh non-empty text wins" "full final text" text
    | other -> fail ("expected AssistantSnapshot, got " + string other)

// ── classifyTurnEvidence: CompletionRequested must yield to non-empty assistant text ──

/// When task_complete fires CompletionRequested "tool marker" but the session
/// idle refresh already captured a real assistant snapshot, the final output must
/// be the assistant text — not the tool marker.  This is the root cause of
/// `continue returns final child output` failures: classifyTurnEvidence matches
/// CompletionRequested first and discards the assistant text unconditionally.
let completionRequestedWithAssistantTextReturnsAssistantText () =
    let evidence =
        { CurrentTurnEvidence.empty with
            Outcome = CompletionRequested "tool marker"
            Assistant = AssistantSnapshot("", 0L, "final assistant output", Some NormalFinish)
            Todos = TodosNotCompleted }

    match classifyTurnEvidence evidence with
    | CompleteNaturally output ->
        check "CompletionRequested must yield to assistant text" (output <> "tool marker")
        equal "output is assistant text, not tool marker" "final assistant output" output
    | other -> fail ("expected CompleteNaturally from assistant text, got " + string other)

/// When CompletionRequested fires with no assistant text, the original tool
/// marker is the correct fallback.
let completionRequestedWithoutAssistantTextReturnsMarker () =
    let evidence =
        { CurrentTurnEvidence.empty with
            Outcome = CompletionRequested "tool marker"
            Assistant = NoAssistant
            Todos = TodosNotCompleted }

    match classifyTurnEvidence evidence with
    | CompleteNaturally output -> equal "fallback to tool marker" "tool marker" output
    | other -> fail ("expected CompleteNaturally fallback, got " + string other)

let run () =
    todosCompletedWithAssistantTextReturnsText ()
    todosCompletedWithoutAssistantTextReturnsEmpty ()
    todosCompletedWithEmptyAssistantReturnsEmpty ()
    completionRequestedWithAssistantTextReturnsAssistantText ()
    completionRequestedWithoutAssistantTextReturnsMarker ()
    mergeAssistantSnapshotEmptyDoesNotOverwriteRealText ()
    mergeAssistantSnapshotNonEmptyOverwritesStreaming ()
