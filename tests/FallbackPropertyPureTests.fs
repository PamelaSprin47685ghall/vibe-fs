module Wanxiangshu.Tests.FallbackPropertyPureTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

let private mkModel () : FallbackModel =
    { ProviderID = "openai"
      ModelID = "gpt-5"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let incrementCancelGeneration_incrementsByOne () =
    let s0 = freshSessionState
    let s1, next = incrementCancelGeneration s0
    equal "snd is 1" 1 next
    equal "CancelGeneration is 1" 1 s1.CancelGeneration

let incrementCancelGeneration_accumulates () =
    let s0 =
        { freshSessionState with
            CancelGeneration = 5 }

    let s1, next = incrementCancelGeneration s0
    equal "snd is 6" 6 next
    equal "CancelGeneration is 6" 6 s1.CancelGeneration

let incrementHumanTurnOrdinal_incrementsByOne () =
    let _, next = incrementHumanTurnOrdinal freshSessionState
    equal "snd is 1" 1 next

let incrementContinuationOrdinal_incrementsByOne () =
    let _, next = incrementContinuationOrdinal freshSessionState
    equal "snd is 1" 1 next

let incrementNudgeOrdinal_incrementsByOne () =
    let _, next = incrementNudgeOrdinal freshSessionState
    equal "snd is 1" 1 next

let incrementCompactionOrdinal_incrementsByOne () =
    let _, next = incrementCompactionOrdinal freshSessionState
    equal "snd is 1" 1 next

let setNudgeActive_then_isNudgeActive () =
    let s = setNudgeActive true freshSessionState
    check "isNudgeActive true" (isNudgeActive s)

let setNudgeActive_false_clears () =
    let s = freshSessionState |> setNudgeActive true |> setNudgeActive false
    check "isNudgeActive false" (not (isNudgeActive s))

let setEventHandlingActive_then_isEventHandlingActive () =
    let s = setEventHandlingActive true freshSessionState
    check "isEventHandlingActive true" (isEventHandlingActive s)

let setMainContinuationAwaitingStart_then_query () =
    let s = setMainContinuationAwaitingStart true freshSessionState
    check "isMainContinuationAwaitingStart true" (isMainContinuationAwaitingStart s)

let tryConsumeNudgeNonce_emptyObserved_returnsFalse () =
    let s, ok = tryConsumeNudgeNonce "" freshSessionState
    check "returns false" (not ok)
    equal "runtime unchanged" freshSessionState s

let tryConsumeNudgeNonce_matchingNonce_returnsTrue () =
    let s0 = armNudgeNonce "abc" freshSessionState
    let s1, ok = tryConsumeNudgeNonce "abc" s0
    check "returns true" ok
    equal "nonce cleared" "" s1.ActiveNudgeNonce

let tryConsumeNudgeNonce_nonMatching_returnsFalse () =
    let s0 = armNudgeNonce "abc" freshSessionState
    let s1, ok = tryConsumeNudgeNonce "xyz" s0
    check "returns false" (not ok)
    equal "nonce preserved" "abc" s1.ActiveNudgeNonce

let advanceHumanTurn_generatesNewId () =
    let _, nextId = advanceHumanTurn freshSessionState
    check "starts with turn-" (nextId.StartsWith "turn-")

let advanceHumanTurn_incrementsOrdinal () =
    let s, _ = advanceHumanTurn freshSessionState
    equal "HumanTurnOrdinal is 1" 1 s.HumanTurnOrdinal

let advanceHumanTurn_incrementsCancelGeneration () =
    let s, _ = advanceHumanTurn freshSessionState
    equal "CancelGeneration is 1" 1 s.CancelGeneration

let selectModel_then_clearModel () =
    let s0 = selectModel (mkModel ()) freshSessionState
    check "Model is Some" s0.Model.IsSome
    let s1 = clearModel s0
    check "Model is None" s1.Model.IsNone

let transferOwnership_changesOwner () =
    let s = transferOwnership SessionOwner.Human freshSessionState
    equal "Owner is Human" SessionOwner.Human s.Owner

let setInjected_setsBothFields () =
    let s = setInjected (mkModel ()) 42L freshSessionState
    check "InjectedModel is Some" s.InjectedModel.IsSome
    equal "InjectedAt is Some 42L" (Some 42L) s.InjectedAt

let clearInjected_clearsBoth () =
    let s = freshSessionState |> setInjected (mkModel ()) 42L |> clearInjected
    check "InjectedModel is None" s.InjectedModel.IsNone
    check "InjectedAt is None" s.InjectedAt.IsNone

let isInjectedSince_thresholdCheck () =
    let s = setInjectedAt 100L freshSessionState
    check "since 50L true" (isInjectedSince 50L s)
    check "since 200L false" (not (isInjectedSince 200L s))

let isTerminalConsumed_initial_false () =
    check "fresh not consumed" (not (isTerminalConsumed freshSessionState))

let setTerminalConsumed_then_query () =
    let s = setTerminalConsumed true freshSessionState
    check "consumed true" (isTerminalConsumed s)
    check "no active episode created" s.ActiveEpisode.IsNone

let setTerminalConsumed_false_clears () =
    let s = freshSessionState |> setTerminalConsumed true |> setTerminalConsumed false
    check "not consumed" (not (isTerminalConsumed s))

let transferOwnership_nonNoOwner_resetsConsumed_and_doesNotCreateEpisode () =
    let s =
        freshSessionState
        |> setTerminalConsumed true
        |> transferOwnership SessionOwner.Human

    equal "Owner is Human" SessionOwner.Human s.Owner
    check "TerminalConsumed reset" (not (isTerminalConsumed s))
    check "ActiveEpisode still None" s.ActiveEpisode.IsNone

let transferOwnership_NoOwner_preservesConsumed () =
    let s =
        freshSessionState
        |> setTerminalConsumed true
        |> transferOwnership SessionOwner.NoOwner

    check "TerminalConsumed preserved" (isTerminalConsumed s)
    equal "Owner NoOwner" SessionOwner.NoOwner s.Owner

let beginHumanTurn_resetsConsumed_and_keepsEpisode () =
    let s = freshSessionState |> setTerminalConsumed true |> beginHumanTurn "msg-1"
    check "TerminalConsumed reset" (not (isTerminalConsumed s))
    check "ActiveEpisode created" s.ActiveEpisode.IsSome

let cancelEpisode_clearsActiveEpisode_and_resetsConsumed () =
    let s =
        freshSessionState
        |> beginHumanTurn "msg-1"
        |> setTerminalConsumed true
        |> cancelEpisode

    check "ActiveEpisode cleared" s.ActiveEpisode.IsNone
    check "TerminalConsumed reset" (not (isTerminalConsumed s))

let run () =
    incrementCancelGeneration_incrementsByOne ()
    incrementCancelGeneration_accumulates ()
    incrementHumanTurnOrdinal_incrementsByOne ()
    incrementContinuationOrdinal_incrementsByOne ()
    incrementNudgeOrdinal_incrementsByOne ()
    incrementCompactionOrdinal_incrementsByOne ()
    setNudgeActive_then_isNudgeActive ()
    setNudgeActive_false_clears ()
    setEventHandlingActive_then_isEventHandlingActive ()
    setMainContinuationAwaitingStart_then_query ()
    tryConsumeNudgeNonce_emptyObserved_returnsFalse ()
    tryConsumeNudgeNonce_matchingNonce_returnsTrue ()
    tryConsumeNudgeNonce_nonMatching_returnsFalse ()
    advanceHumanTurn_generatesNewId ()
    advanceHumanTurn_incrementsOrdinal ()
    advanceHumanTurn_incrementsCancelGeneration ()
    selectModel_then_clearModel ()
    transferOwnership_changesOwner ()
    setInjected_setsBothFields ()
    clearInjected_clearsBoth ()
    isInjectedSince_thresholdCheck ()
    isTerminalConsumed_initial_false ()
    setTerminalConsumed_then_query ()
    setTerminalConsumed_false_clears ()
    transferOwnership_nonNoOwner_resetsConsumed_and_doesNotCreateEpisode ()
    transferOwnership_NoOwner_preservesConsumed ()
    beginHumanTurn_resetsConsumed_and_keepsEpisode ()
    cancelEpisode_clearsActiveEpisode_and_resetsConsumed ()
