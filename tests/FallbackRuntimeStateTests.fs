module Wanxiangshu.Tests.FallbackRuntimeStateTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let mkChain (models: FallbackModel list) = models

let mkModel (pid: string) (mid: string) : FallbackModel =
    { ProviderID = pid
      ModelID = mid
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

// ---------------------------------------------------------------------------
// GetOrCreateState
// ---------------------------------------------------------------------------

let getOrCreate_returnsFreshForNewSession () =
    let rt = FallbackRuntimeState()
    let s = rt.GetOrCreateState "sess-1"
    equal "phase is Idle" FallbackPhase.Idle s.Phase
    equal "failureCount is 0" 0 s.FailureCount
    equal "lifecycle is Active" FallbackLifecycle.Active s.Lifecycle

let getOrCreate_returnsSameStateOnSecondCall () =
    let rt = FallbackRuntimeState()
    let s1 = rt.GetOrCreateState "sess-1"
    let s2 = rt.GetOrCreateState "sess-1"
    equal "same reference" true (obj.ReferenceEquals(s1, s2))

// ---------------------------------------------------------------------------
// UpdateState
// ---------------------------------------------------------------------------

let updateState_persistsChange () =
    let rt = FallbackRuntimeState()
    let s = rt.GetOrCreateState "sess-1"

    let s2 =
        { s with
            FailureCount = 5
            Phase = FallbackPhase.Retrying 1 }

    rt.UpdateState "sess-1" s2
    let s3 = rt.GetOrCreateState "sess-1"
    equal "failureCount updated" 5 s3.FailureCount
    equal "phase updated" (FallbackPhase.Retrying 1) s3.Phase

// ---------------------------------------------------------------------------
// GetChain / SetChain
// ---------------------------------------------------------------------------

let chain_setThenGet () =
    let rt = FallbackRuntimeState()
    let ch = [ mkModel "oai" "gpt-5" ]
    rt.SetChain "sess-1" ch
    let got = rt.GetChain "sess-1"
    equal "chain length" 1 got.Length
    equal "provider" "oai" got.[0].ProviderID

let chain_emptyByDefault () =
    let rt = FallbackRuntimeState()
    equal "empty chain by default" [] (rt.GetChain "unknown-session")

// ---------------------------------------------------------------------------
// SetAgentName / GetAgentName
// ---------------------------------------------------------------------------

let agentName_setThenGet () =
    let rt = FallbackRuntimeState()
    rt.SetAgentName "sess-1" "Sisyphus - Ultraworker"
    equal "agent name" "Sisyphus - Ultraworker" (rt.GetAgentName "sess-1")

let agentName_emptyByDefault () =
    let rt = FallbackRuntimeState()
    equal "empty agent by default" "" (rt.GetAgentName "unknown-session")

// ---------------------------------------------------------------------------
// CleanupSession
// ---------------------------------------------------------------------------

let cleanupSession_removesAllState () =
    let rt = FallbackRuntimeState()
    rt.SetChain "sess-1" [ mkModel "oai" "gpt-5" ]
    rt.SetAgentName "sess-1" "reviewer"
    let s = rt.GetOrCreateState "sess-1"
    rt.UpdateState "sess-1" { s with FailureCount = 7 }
    rt.CleanupSession "sess-1"
    equal "chain gone" [] (rt.GetChain "sess-1")
    equal "agent gone" "" (rt.GetAgentName "sess-1")
    // Re-creating after cleanup gives a fresh state
    let s2 = rt.GetOrCreateState "sess-1"
    equal "state reset" 0 s2.FailureCount

let cleanupSession_doesNotAffectOtherSessions () =
    let rt = FallbackRuntimeState()
    rt.SetChain "sess-1" [ mkModel "oai" "m1" ]
    rt.SetChain "sess-2" [ mkModel "oai" "m2" ]
    rt.CleanupSession "sess-1"
    let ch2 = rt.GetChain "sess-2"
    equal "sess-2 chain intact" 1 ch2.Length
    equal "sess-2 model" "m2" ch2.[0].ModelID

// ---------------------------------------------------------------------------
// Suite entry
// ---------------------------------------------------------------------------

let run () =
    getOrCreate_returnsFreshForNewSession ()
    getOrCreate_returnsSameStateOnSecondCall ()
    updateState_persistsChange ()
    chain_setThenGet ()
    chain_emptyByDefault ()
    agentName_setThenGet ()
    agentName_emptyByDefault ()
    cleanupSession_removesAllState ()
    cleanupSession_doesNotAffectOtherSessions ()
