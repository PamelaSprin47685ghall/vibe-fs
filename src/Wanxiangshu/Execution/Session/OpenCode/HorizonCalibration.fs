namespace Wanxiangshu.Execution.Session.OpenCode

open Wanxiangshu.Participant.Provider

/// Horizon calibration: the four numbers a caller prices a participant with —
/// turns spent and estimated to remain, wall-clock spent, and the time that
/// remains at the pace that participant has shown. It owns its own prose so the
/// roster tool can state it without depending on the roster surface.
[<RequireQualifiedAccess>]
module HorizonCalibration =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Calibration = "tool/horizon/calibration"

    let render
        (language: string)
        (label: string)
        (turnsUsed: int)
        (turnsRemaining: int)
        (elapsedMilliseconds: float)
        : string =
        let language = ProviderLanguage.parse language

        let remainingMilliseconds =
            elapsedMilliseconds * float turnsRemaining / float (max 1 turnsUsed)

        ProviderProse.render
            language
            Path.Calibration
            (Map
                [ "label", label
                  "turns_used", string turnsUsed
                  "turns_left", string turnsRemaining
                  "time_used", DurationLabel.format language elapsedMilliseconds
                  "time_left", DurationLabel.format language remainingMilliseconds ])
