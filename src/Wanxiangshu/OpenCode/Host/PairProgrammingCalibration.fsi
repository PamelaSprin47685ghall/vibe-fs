namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module PairProgrammingCalibration =
    [<Literal>]
    val ToolEstimatePath: string = "host/pair-programming-tool-estimate"

    [<Literal>]
    val ElapsedPath: string = "host/pair-programming-elapsed"

    val document: tip: string option -> toolEstimate: string option -> guideline: string -> LlmFacing.Document

    val documentWithElapsed:
        tip: string option ->
        elapsed: string option ->
        toolEstimate: string option ->
        guideline: string ->
            LlmFacing.Document

    val compose: tip: string option -> toolEstimate: string option -> guideline: string -> string

    val composeWithElapsed:
        tip: string option -> elapsed: string option -> toolEstimate: string option -> guideline: string -> string

    val renderToolEstimate: language: ProviderLanguage -> remaining: int -> string
    val renderElapsed: language: ProviderLanguage -> elapsedMilliseconds: float -> string
