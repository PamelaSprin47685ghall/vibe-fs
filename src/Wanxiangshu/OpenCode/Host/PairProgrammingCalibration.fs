namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module PairProgrammingCalibration =

    [<Literal>]
    let BudgetPath = "host/pair-programming-budget"

    [<Literal>]
    let ElapsedPath = "host/pair-programming-elapsed"

    [<Literal>]
    let TokenEconomyPath = "host/pair-programming-token-economy"

    [<Literal>]
    let ContextLimitPath = "host/pair-programming-context-limit"

    let private nonBlank (value: string option) =
        value
        |> Option.map (fun text -> text.Trim())
        |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let private instructionTexts fragments =
        fragments |> List.map nonBlank |> List.choose id

    let document tip toolEstimate guideline =
        LlmFacing.instructions (instructionTexts [ tip; toolEstimate; Some guideline ])

    let documentWithElapsed tip elapsed toolEstimate guideline =
        LlmFacing.instructions (instructionTexts [ tip; elapsed; toolEstimate; Some guideline ])

    /// The per-round Pair Hint, including the state fragments the model needs
    /// to price its own view declarations: last round's token economy as the
    /// provider billed it, and the declared context limit.
    let documentWithState tip elapsed state calibration guideline =
        LlmFacing.instructions (instructionTexts (tip :: elapsed :: (state @ [ calibration; Some guideline ])))

    let compose tip toolEstimate guideline =
        document tip toolEstimate guideline |> LlmFacing.render

    let composeWithElapsed tip elapsed toolEstimate guideline =
        documentWithElapsed tip elapsed toolEstimate guideline |> LlmFacing.render

    let renderElapsed language elapsedMilliseconds =
        ProviderProse.render language ElapsedPath (Map [ "elapsed", DurationLabel.format language elapsedMilliseconds ])

    /// The four calibration numbers the model prices its own work with: turns
    /// spent and estimated to remain, wall-clock spent, and the time that
    /// remains at the pace actually shown (the average cost of the turns
    /// already spent, times the turns that remain).
    let renderBudget language (turnsUsed: int) (turnsRemaining: int) (elapsedMilliseconds: float) =
        let remainingMilliseconds =
            elapsedMilliseconds * float turnsRemaining / float (max 1 turnsUsed)

        ProviderProse.render
            language
            BudgetPath
            (Map
                [ "turns_used", string turnsUsed
                  "turns_left", string turnsRemaining
                  "time_used", DurationLabel.format language elapsedMilliseconds
                  "time_left", DurationLabel.format language remainingMilliseconds ])

    /// Last request's own accounting, as the provider reported it: the tokens
    /// it served from the cache prefix, and the tokens it recomputed.
    let renderTokenEconomy language (cached: int) (uncached: int) =
        ProviderProse.render language TokenEconomyPath (Map [ "cached", string cached; "uncached", string uncached ])

    let renderContextLimit language (limit: int) =
        ProviderProse.render language ContextLimitPath (Map [ "limit", string limit ])
