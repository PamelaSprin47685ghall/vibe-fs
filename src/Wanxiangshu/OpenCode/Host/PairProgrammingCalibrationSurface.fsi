namespace Wanxiangshu.OpenCode.Host

[<RequireQualifiedAccess>]
module PairProgrammingCalibrationSurface =
    val compose: tip: obj -> toolEstimate: obj -> guideline: string -> string
    val composeWithElapsed: tip: obj -> elapsed: obj -> toolEstimate: obj -> guideline: string -> string
    val renderToolEstimate: language: string -> remaining: obj -> string
    val renderElapsed: language: string -> elapsedMilliseconds: obj -> string
    val renderTokenEconomy: language: string -> cached: obj -> uncached: obj -> string
    val renderContextLimit: language: string -> limit: obj -> string
