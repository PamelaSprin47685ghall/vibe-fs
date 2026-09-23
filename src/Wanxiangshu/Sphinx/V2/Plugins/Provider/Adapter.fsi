namespace Wanxiangshu.Sphinx.V2.Plugins

open System
open Wanxiangshu.Sphinx.V2.Core

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
    /// The token and call counts one outcome contributes.
    val usageCountsOf: ProviderOutcome -> int64 * int64 * int64

    /// Whether an outcome can be accepted at all.
    val admissible: ProviderOutcome -> Result<unit, ProviderAdapterFault>

    /// Whether the outcome must keep its reservation booked because usage is unknown.
    val keepsReservation: ProviderOutcome -> bool

    /// A settled usage record for one work item, derived from the outcome.
    val settledUsageOf: WorkId -> Attempt -> ProviderOutcome -> SettledUsage
