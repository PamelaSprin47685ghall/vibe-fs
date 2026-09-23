namespace Wanxiangshu.Sphinx.V2.Runtime

open System
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Plugins

/// The JS-native surface for the decision loop. Pure: no store, no clock, no model call.
///
/// A caller supplies plain records. `Location` is NaN when absent, `Rank` is -1 when
/// the plan was never compared, and `Kind` is a plain string naming the estimate kind.
/// `Rank` alone decides.
module Surface =
    // Decision
    val decisionRank: string -> obj list -> obj array
    val decisionSupportsNumeric: obj list -> bool
    val decisionSelect: string -> obj list -> obj
    val decisionExclude: string -> string -> obj

    // Default profile
    val profileDefault: unit -> DefaultProfile
    val profileValidate: DefaultProfile -> Result<DefaultProfile, ProfileError>
    val profileConfigInput: unit -> string
    val profileClaimsIndependence: DefaultProfile -> bool
    val profileWith: obj -> DefaultProfile

    // Stop
    val stopRanked: unit -> obj
    val stopOrdinal: unit -> obj
    val stopResourceLimited: unit -> obj
    val stopNoPlan: unit -> obj
    val stopCancelled: unit -> obj
    val stopCertified: string -> obj
    val stopReasonName: obj -> string
    val stopIsModelRelative: obj -> bool

    /// Classify a state into the next action, reported as plain values.
    val classifyOutcome: InquiryState -> obj

    // Provider usage
    val providerOutcome: string -> int64 -> int64 -> int64 -> bool -> Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome
    val providerUsageUnresolved: Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome -> bool
    val providerUsageCounts: Wanxiangshu.Sphinx.V2.Plugins.ProviderOutcome -> int64 * int64 * int64

    // Recovery
    val recoveryAction: string -> string
    val recoveryMaySpend: string -> bool
