module Wanxiangshu.Tests.ResourcePlanTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.ResourcePlan

// ── Helpers ──

let private sid = SessionId.create "s1"
let private rid = RunId.create "r1"
let private tid1 = TurnId.create "t1"

let private fallbackModel: FallbackModel =
    { ProviderID = "p"
      ModelID = "m"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private fallbackChain: FallbackChain = []

let private fallbackConfig: FallbackConfig =
    { DefaultChain = fallbackChain
      AgentChains = Map.empty
      MaxRetries = 3
      LoopMaxContinues = 5
      MaxRecoveries = 3 }

let private policy: FallbackPolicyState =
    { Selection = StableAt 0
      FailureCount = 0
      ContinueCount = 0
      RecoveryCount = 0 }

let private runCtx: RunContext =
    { RunId = rid
      ParentSessionId = sid
      SessionId = sid
      Policy = policy
      FallbackConfig = fallbackConfig
      Chain = fallbackChain
      NextTurnOrdinal = TurnOrdinal.first }

let private turnPlan: TurnPlan =
    { TurnId = tid1
      Ordinal = TurnOrdinal.first
      Model = Some fallbackModel
      Prompt = "do work" }

let private startedTurn: StartedTurn =
    { Plan = turnPlan
      StartReceipt = UserMessageObserved "m1" }

let private abortCtx: AbortContext =
    { Reason = UserRequested
      AfterStop = FinishCancelled }

let private cancelCtx: CancelContext = abortCtx

let private nowMs = 1000000L

// ── Tests for projectResources ──

let availableProducesNoResources () =
    let state = Available { SessionId = sid }
    let specs = projectResources state
    check "Available → no resources" (List.isEmpty specs)

let poisonedProducesNoResources () =
    let state = Poisoned(PoisonReason.HostProtocolBroken "test")
    let specs = projectResources state
    check "Poisoned → no resources" (List.isEmpty specs)

let dispatchingProducesTurnDeadline () =
    let turnDeadlineAtMs = nowMs + 300_000L

    let state =
        Dispatching(runCtx, turnPlan, CurrentTurnEvidence.empty, PendingTerminal.empty, turnDeadlineAtMs)

    let specs = projectResources state
    equal "Dispatching → 1 resource" 1 specs.Length

    match specs.[0] with
    | TurnDeadline(id, deadline) ->
        check
            "Dispatching: resource id is TurnDeadlineId"
            (match id with
             | TurnDeadlineId t -> t = TurnId.value tid1
             | _ -> false)

        equal "Dispatching: deadline = now + 300s" (nowMs + 300_000L) deadline.DeadlineAtMs
    | _ -> check "Dispatching → expected TurnDeadline" false

let runningProducesTurnDeadline () =
    let turnDeadlineAtMs = nowMs + 300_000L

    let state =
        Running(runCtx, startedTurn, CurrentTurnEvidence.empty, turnDeadlineAtMs)

    let specs = projectResources state
    equal "Running → 1 resource" 1 specs.Length

    match specs.[0] with
    | TurnDeadline(id, deadline) ->
        check
            "Running: resource id is TurnDeadlineId"
            (match id with
             | TurnDeadlineId t -> t = TurnId.value tid1
             | _ -> false)

        equal "Running: deadline = now + 300s" (nowMs + 300_000L) deadline.DeadlineAtMs
    | _ -> check "Running → expected TurnDeadline" false

let issuingAbortProducesAbortDeadline () =
    let abortDeadlineAtMs = nowMs + 60_000L

    let state =
        IssuingAbort(runCtx, Started startedTurn, abortCtx, false, abortDeadlineAtMs)

    let specs = projectResources state
    equal "IssuingAbort → 1 resource" 1 specs.Length

    match specs.[0] with
    | AbortDeadline(id, deadline) ->
        check
            "IssuingAbort: resource id is AbortDeadlineId"
            (match id with
             | AbortDeadlineId t -> t = TurnId.value tid1
             | _ -> false)

        equal "IssuingAbort: deadline = now + 60s" (nowMs + 60_000L) deadline.DeadlineAtMs
    | _ -> check "IssuingAbort → expected AbortDeadline" false

let reconcilingProducesTwoDeadlines () =
    let turnDeadlineAtMs = nowMs + 300_000L
    let reconciliationDeadlineAtMs = nowMs + 30_000L

    let state =
        ReconcilingUnknownDispatch(runCtx, turnPlan, cancelCtx, 0, turnDeadlineAtMs, reconciliationDeadlineAtMs)

    let specs = projectResources state
    equal "ReconcilingUnknownDispatch → 2 resources" 2 specs.Length

    let hasTurnDeadline =
        specs
        |> List.exists (function
            | TurnDeadline(TurnDeadlineId t, _) -> t = TurnId.value tid1
            | _ -> false)

    let hasReconDeadline =
        specs
        |> List.exists (function
            | ReconciliationDeadline(ReconciliationDeadlineId t, _) -> t = TurnId.value tid1
            | _ -> false)

    check "has turn deadline" hasTurnDeadline
    check "has reconciliation deadline" hasReconDeadline

// ── Tests for diffResources ──

let diffFromEmptyToTurnDeadline () =
    let prev: ResourceSpec list = []

    let next =
        [ TurnDeadline(TurnDeadlineId(TurnId.value tid1), { DeadlineAtMs = nowMs + 300_000L }) ]

    let diff = diffResources prev next
    equal "acquire 1" 1 diff.ToAcquire.Length
    equal "release 0" 0 diff.ToRelease.Length

let diffFromTurnDeadlineToEmpty () =
    let prev =
        [ TurnDeadline(TurnDeadlineId(TurnId.value tid1), { DeadlineAtMs = nowMs + 300_000L }) ]

    let next: ResourceSpec list = []
    let diff = diffResources prev next
    equal "acquire 0" 0 diff.ToAcquire.Length
    equal "release 1" 1 diff.ToRelease.Length

let diffSameResourceUnchanged () =
    let spec =
        TurnDeadline(TurnDeadlineId(TurnId.value tid1), { DeadlineAtMs = nowMs + 300_000L })

    let diff = diffResources [ spec ] [ spec ]
    equal "acquire 0 (same)" 0 diff.ToAcquire.Length
    equal "release 0 (same)" 0 diff.ToRelease.Length

let diffDifferentIdentity () =
    let t1 =
        TurnDeadline(TurnDeadlineId(TurnId.value tid1), { DeadlineAtMs = nowMs + 300_000L })

    let t2 = TurnDeadline(TurnDeadlineId "other", { DeadlineAtMs = nowMs + 300_000L })
    let diff = diffResources [ t1 ] [ t2 ]
    equal "acquire 1 (different id)" 1 diff.ToAcquire.Length
    equal "release 1 (different id)" 1 diff.ToRelease.Length

let run () =
    availableProducesNoResources ()
    poisonedProducesNoResources ()
    dispatchingProducesTurnDeadline ()
    runningProducesTurnDeadline ()
    issuingAbortProducesAbortDeadline ()
    reconcilingProducesTwoDeadlines ()
    diffFromEmptyToTurnDeadline ()
    diffFromTurnDeadlineToEmpty ()
    diffSameResourceUnchanged ()
    diffDifferentIdentity ()
