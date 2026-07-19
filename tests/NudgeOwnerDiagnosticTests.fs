module Wanxiangshu.Tests.NudgeOwnerDiagnosticTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.NudgeTriggerOps

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

        do! rmAsync dir
    }

let run () =
    promise { do! resolveOwner_emitsNudgeOwnerUnknownInProduction () }
