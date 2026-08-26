// primary_owner: time-capability — TimeCapability.SurfaceSurface — KEEP — pair calibration surface
namespace Wanxiangshu.OpenCode.Host

open Fable.Core.JsInterop
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

/// JS-native owner boundary for pair-guidance assembly and localized dynamic
/// estimate prose. Optional fragments and provider language cross as strings;
/// PairProgrammingCalibration retains composition and localization semantics.
[<RequireQualifiedAccess>]
module PairProgrammingCalibrationSurface =

    let private isNullish (value: obj) : bool =
        isNull value || emitJsExpr value "$0 === undefined"

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(string value)

    let private languageOf (raw: string) : ProviderLanguage =
        match raw with
        | "SimplifiedChinese"
        | "zh-CN" -> ProviderLanguage.SimplifiedChinese
        | "English" -> ProviderLanguage.English
        | _ -> ProviderLanguage.parse raw

    let compose (tip: obj) (toolEstimate: obj) (guideline: string) : string =
        PairProgrammingCalibration.compose (optionalText tip) (optionalText toolEstimate) guideline

    let composeWithElapsed (tip: obj) (elapsed: obj) (toolEstimate: obj) (guideline: string) : string =
        PairProgrammingCalibration.composeWithElapsed
            (optionalText tip)
            (optionalText elapsed)
            (optionalText toolEstimate)
            guideline

    let renderToolEstimate (language: string) (remaining: obj) : string =
        PairProgrammingCalibration.renderToolEstimate (languageOf language) (int64 (string remaining))

    let renderElapsed (language: string) (elapsedMilliseconds: obj) : string =
        PairProgrammingCalibration.renderElapsed (languageOf language) (float (string elapsedMilliseconds))
