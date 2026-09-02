namespace Wanxiangshu.Strength.OpenCode

open System
open Wanxiangshu.Strength

/// STRENGTH-010/011: Host-owned rollout settings. Malformed or incomplete cost
/// metadata never guesses; treatment collapses to K0 through Costs=None.
[<RequireQualifiedAccess>]
module StrengthSettings =

    let HostCanaryFingerprint =
        "opencode-ai@1.18.18|@opencode-ai/plugin@>=1.17.4|strength-host-canary-v1"

    let private env name =
        match Environment.GetEnvironmentVariable name with
        | null -> None
        | value when String.IsNullOrWhiteSpace value -> None
        | value -> Some(value.Trim())

    let private nonNegativeFloat (name: string) =
        env name
        |> Option.bind (fun (text: string) ->
            match Double.TryParse text with
            | true, value when value >= 0.0 && value <= Double.MaxValue -> Some value
            | _ -> None)

    let private parseNonNegativeInt (fallback: int) (text: string) =
        match Int32.TryParse text with
        | true, value when value >= 0 -> value
        | _ -> fallback

    let private nonNegativeInt name fallback =
        match env name with
        | None -> fallback
        | Some text -> parseNonNegativeInt fallback text

    let private mode () =
        match
            env "WANXIANGSHU_STRENGTH_MODE"
            |> Option.map (fun value -> value.ToLowerInvariant())
        with
        | Some "off" -> StrengthRolloutMode.Off
        | Some "dry-run" -> StrengthRolloutMode.DryRun
        | Some "treatment" -> StrengthRolloutMode.Treatment
        | Some "shadow"
        | None -> StrengthRolloutMode.Shadow
        | Some _ -> StrengthRolloutMode.Off

    let private buildCosts (values: float option list) =
        match values with
        | [ Some savedDeep1
            Some savedDeep2
            Some fast1
            Some fast2
            Some byte1
            Some byte2
            Some delay1
            Some delay2
            Some risk1
            Some risk2 ] ->
            Some
                { SavedDeep1 = savedDeep1
                  SavedDeep2 = savedDeep2
                  Fast1 = fast1
                  Fast2 = fast2
                  Byte1 = byte1
                  Byte2 = byte2
                  Delay1 = delay1
                  Delay2 = delay2
                  Risk1 = risk1
                  Risk2 = risk2 }
        | _ -> None

    let private costs () =
        let values =
            [ nonNegativeFloat "WANXIANGSHU_STRENGTH_SAVED_DEEP_1"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_SAVED_DEEP_2"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_FAST_1"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_FAST_2"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_BYTE_1"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_BYTE_2"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_DELAY_1"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_DELAY_2"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_RISK_1"
              nonNegativeFloat "WANXIANGSHU_STRENGTH_RISK_2" ]

        if values |> List.forall Option.isSome then
            buildCosts values
        else
            None

    let hostCanaryHealthy () =
        match env "WANXIANGSHU_STRENGTH_HOST_CANARY" with
        | Some value -> String.Equals(value, HostCanaryFingerprint, StringComparison.Ordinal)
        | None -> false

    /// Host-canary only. DryRun never publishes Prepared or changes primary bytes,
    /// so K2 can be exercised independently of treatment/economic activation.
    /// Missing or malformed input stays on the established K1 canary path.
    let dryRunBudget () : StrengthBudget =
        match env "WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET" |> Option.bind StrengthBudget.parse with
        | Some StrengthBudget.K2 -> StrengthBudget.K2
        | _ -> StrengthBudget.K1

    let load () : StrengthRolloutConfig =
        let k1Margin =
            nonNegativeFloat "WANXIANGSHU_STRENGTH_K1_MARGIN" |> Option.defaultValue 0.0

        let k2Margin =
            nonNegativeFloat "WANXIANGSHU_STRENGTH_K2_MARGIN" |> Option.defaultValue 0.25

        let safeK2Margin = max (k1Margin + 0.000001) k2Margin

        { Mode = mode ()
          PolicyVersion = env "WANXIANGSHU_STRENGTH_POLICY_VERSION" |> Option.defaultValue "strength-v1"
          ControlRateBasisPoints = min 10000 (nonNegativeInt "WANXIANGSHU_STRENGTH_CONTROL_BPS" 1000)
          Policy =
            { K1Margin = k1Margin
              K2Margin = safeK2Margin
              K2MinimumEvidence = nonNegativeInt "WANXIANGSHU_STRENGTH_K2_MIN_EVIDENCE" 50 }
          Costs = costs () }
