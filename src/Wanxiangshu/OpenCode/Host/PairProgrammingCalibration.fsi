namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module PairProgrammingCalibration =

    val document: tip: string option -> toolEstimate: string option -> guideline: string -> LlmFacing.Document

    /// The per-round Pair Hint including the state fragments: last round's
    /// token economy and the declared context limit.
    val documentWithState:
        tip: string option ->
        elapsed: string option ->
        state: string option list ->
        calibration: string option ->
        guideline: string ->
            LlmFacing.Document

    val compose: tip: string option -> toolEstimate: string option -> guideline: string -> string

    val composeWithElapsed:
        tip: string option -> elapsed: string option -> toolEstimate: string option -> guideline: string -> string

    val renderElapsed: language: ProviderLanguage -> elapsedMilliseconds: float -> string

    val renderBudget:
        language: ProviderLanguage -> turnsUsed: int -> turnsRemaining: int -> elapsedMilliseconds: float -> string

    val renderTokenEconomy: language: ProviderLanguage -> cached: int -> uncached: int -> string
    val renderContextLimit: language: ProviderLanguage -> limit: int -> string
