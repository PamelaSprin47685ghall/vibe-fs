namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module PairProgrammingCalibration =
    [<Literal>]
    val ToolEstimatePath: string = "host/pair-programming-tool-estimate"

    [<Literal>]
    val ElapsedPath: string = "host/pair-programming-elapsed"

    [<Literal>]
    val TokenEconomyPath: string = "host/pair-programming-token-economy"

    [<Literal>]
    val ContextLimitPath: string = "host/pair-programming-context-limit"

    val document: tip: string option -> toolEstimate: string option -> guideline: string -> LlmFacing.Document

    val documentWithElapsed:
        tip: string option ->
        elapsed: string option ->
        toolEstimate: string option ->
        guideline: string ->
            LlmFacing.Document

    /// The per-round Pair Hint including the state fragments: last round's
    /// token economy and the declared context limit.
    val documentWithState:
        tip: string option ->
        elapsed: string option ->
        state: string option list ->
        toolEstimate: string option ->
        guideline: string ->
            LlmFacing.Document

    val compose: tip: string option -> toolEstimate: string option -> guideline: string -> string

    val composeWithElapsed:
        tip: string option -> elapsed: string option -> toolEstimate: string option -> guideline: string -> string

    val renderToolEstimate: language: ProviderLanguage -> remaining: int -> string
    val renderElapsed: language: ProviderLanguage -> elapsedMilliseconds: float -> string
    val renderTokenEconomy: language: ProviderLanguage -> cached: int -> uncached: int -> string
    val renderContextLimit: language: ProviderLanguage -> limit: int -> string
