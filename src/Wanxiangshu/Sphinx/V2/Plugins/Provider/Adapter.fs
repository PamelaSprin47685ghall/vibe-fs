namespace Wanxiangshu.Sphinx.V2.Plugins

open System
open Wanxiangshu.Sphinx.V2.Core

/// The provider adapter's contract, expressed against the real Host seam.
///
/// WHAT[sphinx-v2-013]: usage is recorded as the Host reports it. A provider that never
/// returns usage is `unresolved` and the reservation stays booked — never written as
/// zero, which would let the ledger claim an expensive call was free.
///
/// WHAT[sphinx-v2-034]: an overrun is a recorded fact. This adapter never drops usage
/// to make the numbers balance.
/// One provider call's outcome. `UsageUnresolved` is distinct from "zero usage": the
/// first means the Host did not tell us, the second means nothing was consumed.
type ProviderOutcome =
    {
        Text: string
        InputTokens: int64
        OutputTokens: int64
        Calls: int64
        MoneyMinor: int64
        UsageUnresolved: bool
        /// The physical run the Host reported back, when it reported one.
        PhysicalRunRef: string option
    }

[<RequireQualifiedAccess>]
type ProviderAdapterFault =
    | NoPhysicalRun
    | UsageUnresolved
    | ProviderFailure of reason: string

module ProviderAdapter =

    let private error fault = Error fault

    /// The token and call counts one outcome contributes. A call count of zero means
    /// the provider did not report how many calls it made, which is a fact to carry
    /// rather than a number to guess.
    let usageCountsOf (outcome: ProviderOutcome) : int64 * int64 * int64 =
        (outcome.InputTokens, outcome.OutputTokens, outcome.Calls)

    /// Whether an outcome can be accepted at all. A missing physical run means the Host
    /// never actually ran, so there is nothing to accept.
    let admissible (outcome: ProviderOutcome) : Result<unit, ProviderAdapterFault> =
        match String.IsNullOrWhiteSpace outcome.Text, Option.isSome outcome.PhysicalRunRef with
        | true, _ -> error (ProviderAdapterFault.ProviderFailure "provider returned no text")
        | false, false -> error ProviderAdapterFault.NoPhysicalRun
        | false, true -> Ok()

    /// Whether the outcome must keep its reservation booked because the provider's usage
    /// figure is missing.
    let keepsReservation (outcome: ProviderOutcome) : bool = outcome.UsageUnresolved

    /// A settled usage record for one work item, derived from the outcome.
    let settledUsageOf (workId: WorkId) (attempt: Attempt) (outcome: ProviderOutcome) : SettledUsage =
        { WorkId = workId
          Attempt = attempt
          Resources =
            [ "inputTokens", float outcome.InputTokens
              "outputTokens", float outcome.OutputTokens
              "calls", float outcome.Calls ]
            |> Map.ofList
          MoneyMinor = Some outcome.MoneyMinor
          UsageUnresolved = keepsReservation outcome
          Overrun = false }
