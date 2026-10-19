namespace Wanxiangshu.Execution.Session.OpenCode

/// Horizon calibration: the four numbers a caller prices a participant with.
[<RequireQualifiedAccess>]
module HorizonCalibration =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Calibration: string = "tool/horizon/calibration"

    val render:
        language: string ->
        label: string ->
        turnsUsed: int ->
        turnsRemaining: int ->
        elapsedMilliseconds: float ->
            string
