module Wanxiangshu.Tests.NudgeOwnerDiagnosticTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps

/// N-06: production owner inference failure is a diagnostic state, not a
/// silent permanent NoOwner dead zone. Must emit durable nudge_owner_unknown
/// with structured feature/session/reason fields.
let resolveOwner_emitsNudgeOwnerUnknownInProduction () =
    promise {
        let! dir = mkdtempAsync "nudge-owner-unknown-"
        let ctx = createObj [ "directory", box dir ]
        let runtime = FallbackRuntimeStore()
        let sessionID = "s-owner-unknown"

        let! owner = resolveOwner ctx runtime sessionID false

        equal "owner is NoOwner" SessionOwner.NoOwner owner

        let! contentOpt = tryReadFileAsync (dir + "/.wanxiangshu.ndjson")

        match contentOpt with
        | None -> failwith "Expected .wanxiangshu.ndjson to be created"
        | Some content ->
            check "event kind nudge_owner_unknown" (content.Contains("nudge_owner_unknown"))
            check "session field present" (content.Contains("\"session\":\"" + sessionID + "\""))
            check "feature nudge present" (content.Contains("\"feature\":\"nudge\""))
            check "reason field present" (content.Contains("\"reason\""))
            check "event field present" (content.Contains("\"event\":\"nudge_owner_unknown\""))

        do! rmAsync dir
    }

/// N-06: known owner is returned as-is; no owner-unknown diagnostic noise.
let resolveOwner_knownOwnerSkipsDiagnostic () =
    promise {
        let! dir = mkdtempAsync "nudge-owner-known-"
        let ctx = createObj [ "directory", box dir ]
        let runtime = FallbackRuntimeStore()
        let sessionID = "s-owner-known"

        runtime.UpdateSession(
            sessionID,
            Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure.transferOwnership SessionOwner.Human
        )

        let! owner = resolveOwner ctx runtime sessionID false
        equal "owner is Human" SessionOwner.Human owner

        let! contentOpt = tryReadFileAsync (dir + "/.wanxiangshu.ndjson")

        match contentOpt with
        | None -> check "no diagnostic file when owner known" true
        | Some content -> check "no owner_unknown when owner known" (not (content.Contains("nudge_owner_unknown")))

        do! rmAsync dir
    }

let run () =
    promise {
        do! resolveOwner_emitsNudgeOwnerUnknownInProduction ()
        do! resolveOwner_knownOwnerSkipsDiagnostic ()
    }
