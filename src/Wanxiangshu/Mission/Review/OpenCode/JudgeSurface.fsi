namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// Public judgement-tool owner boundary. It exposes the exact parser/schema and
/// contract view; ToolRuntimeScope and HostToolCodec remain implementation-only.
[<RequireQualifiedAccess>]
module JudgeSurface =

    val schemaJson: string

    val parse: value: string -> obj

    val contract: language: string -> obj

    val decideExecution:
        role: string ->
        sessionId: string ->
        isSubmitted: bool ->
        verdict: string ->
        toolCallId: string ->
        providerRunId: string ->
        physicalUserMessageId: string ->
            obj

    val receipt: language: string -> string

    val alreadyJudged: language: string -> string

    val markVerdictSubmitted: sessionId: string -> physicalUserMessageId: string -> unit

    val hasVerdictSubmitted: sessionId: string -> physicalUserMessageId: string -> bool

    val clearVerdictSubmissions: unit -> unit

    val ensureSubmittedAttemptClosed: handle: JournalHandle -> sessionId: string -> Task<obj>

    val interruptAfterSubmittedJudgement:
        handle: JournalHandle -> physicalUserMessageId: string -> sessionPort: obj -> sessionId: string -> Task<obj>
