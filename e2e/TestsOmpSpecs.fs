module Wanxiangshu.E2e.OmpTestsSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes
open Wanxiangshu.E2e.OmpToolRegistryAndCommandsTests
open Wanxiangshu.E2e.TestsOmpSpecsLifecycle

let testSpecs (h: OmpHarness) (ok: int ref) : JS.Promise<unit> =
    promise {
        let chk l c =
            check l c

            if c then
                ok.Value <- ok.Value + 1

        do! runOmpToolRegistry h chk
        do! runOmpCommandsAndHandlers h chk

        let sessionId = "e2e-omp-session-1"
        let! _ = withTimeout (h.emitEvent "session_start" (createObj [ "reason", box "start" ]) sessionId)
        let! _ = withTimeout (h.emitEvent "turn_start" (createObj []) sessionId)

        do! runOmpFuzzyTools h chk sessionId
        do! runOmpExecutorTools h chk sessionId
        do! runOmpWebTools h chk sessionId
        do! runOmpAgentTools h chk sessionId
        do! runOmpMethodology h chk sessionId
        do! runOmpInvestigator h chk sessionId
        do! runOmpTodowrite h chk sessionId
        do! runOmpLoopCommand h chk sessionId
        do! runOmpReview h chk sessionId
        do! runOmpLoopReview h chk sessionId
        do! runOmpShutdown h chk sessionId
    }
