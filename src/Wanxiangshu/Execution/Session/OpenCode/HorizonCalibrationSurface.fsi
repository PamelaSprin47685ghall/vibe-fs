namespace Wanxiangshu.Execution.Session.OpenCode

/// participant-horizon-[011]: JS-native owner boundary for the roster tool's calibration
/// prose. The four numbers cross as opaque JS values; `HorizonCalibration` keeps the
/// arithmetic and the authored-resource localization.
[<RequireQualifiedAccess>]
module HorizonCalibrationSurface =
    val render:
        language: string -> label: string -> turnsUsed: obj -> turnsLeft: obj -> elapsedMilliseconds: obj -> string
