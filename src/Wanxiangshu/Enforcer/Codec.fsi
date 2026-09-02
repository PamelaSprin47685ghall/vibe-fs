namespace Wanxiangshu.Enforcer

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open System

module EnforcerCodec =

    type CanonicalBlogCall =
        { Text: string option
          Evidence: string option
          Tip: EnforcerTip }

    [<Literal>]
    val MissingTipError: string = "missing required argument: tip"

    val decodeCall: Wanxiangshu.Enforcer.EnforcerRule list -> Map<string, obj> -> Result<CanonicalBlogCall, string>
    val hasValidText: CanonicalBlogCall -> bool
