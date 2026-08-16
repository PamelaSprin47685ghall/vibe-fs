namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

open Wanxiangshu.Enforcer.EnforcerCodec

module EnforcerCycle =

    type CanonicalCycle =
        { MergedText: string
          CanonicalTip: EnforcerTip
          MergedEvidence: string }

    let ofCall (call: CanonicalBlogCall) : CanonicalCycle =
        { MergedText = call.Text |> Option.defaultValue ""
          CanonicalTip = call.Tip
          MergedEvidence = call.Evidence |> Option.defaultValue "" }

    let isValidCycle (cycle: CanonicalCycle) : bool = cycle.MergedText.Trim().Length > 0
