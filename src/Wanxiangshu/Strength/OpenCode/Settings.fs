namespace Wanxiangshu.Strength.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

open System
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// STRENGTH-010/011: Host-owned rollout settings. Malformed or incomplete cost
/// metadata never guesses; treatment collapses to K0 through Costs=None.
[<RequireQualifiedAccess>]
module StrengthSettings =

    let HostCanaryFingerprint =
        "opencode-ai@1.18.14|@opencode-ai/plugin@>=1.17.4|strength-host-canary-v1"

    let private env name =
        match Environment.GetEnvironmentVariable name with
        | null -> None
        | value when String.IsNullOrWhiteSpace value -> None
        | value -> Some(value.Trim())

    let private nonNegativeFloat name =
        env name
        |> Option.bind (fun text ->
            match Double.TryParse text with
            | true, value when value >= 0.0 && value <= Double.MaxValue -> Some value
            | _ -> None)

    let private nonNegativeInt name fallback =
        match env name with
        | None -> fallback
        | Some text ->
            match Int32.TryParse text with
            | true, value when value >= 0 -> value
            | _ -> fallback

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
            let unwrapped = values |> List.map Option.get

            match unwrapped with
            | [ savedDeep1; savedDeep2; fast1; fast2; byte1; byte2; delay1; delay2; risk1; risk2 ] ->
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
