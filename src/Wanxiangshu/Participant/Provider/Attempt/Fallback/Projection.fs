namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Foundation.Identity
open Fable.Core
open Fable.Core.JsInterop

type ObservedProviderFailure =
    { Key: string
      Count: int
      Reason: string }

type ProviderFailureProjection =
    { LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      Budget: ProviderFailureBudget.FailureBudget
      BudgetSpec: ProviderFailureBudgetSpec option
      ObservedFailures: ObservedProviderFailure list
      SuccessKeys: string list
      Exhausted: bool
      LastTransitionWasSuccess: bool }

/// Why a failure record was not applied.
type ProviderFailureAdvanceRejection =
    | AlreadyObserved
    | AlreadyExhausted
    | DifferentRun
    | NoActiveBudget
    | InvalidTransition
    | InvalidBudget
    | ConflictingObservation

[<RequireQualifiedAccess>]
type ProviderFailureReplay =
    | RetryAuthorized
    | RetryExhausted
    | EpisodeSuperseded

[<RequireQualifiedAccess>]
type ProviderFailureObservation =
    | Record of ProviderFailureBudgetSpec * int * ProviderFailureProjection
    | Replay of ProviderFailureReplay

module ProviderFailureProjection =

    [<Import("ProviderFailureBudget", "wanxiangli/failure-budget")>]
    let private budgetLibraryClass: obj = jsNative

    let private library: obj = emitJsExpr budgetLibraryClass "new $0()"

    let private specView (spec: ProviderFailureBudgetSpec) =
        createObj [ "version" ==> spec.Version; "limit" ==> spec.Limit ]

    let private specOf (value: obj) : ProviderFailureBudgetSpec =
        { Version = unbox<int> value?version
          Limit = unbox<int> value?limit }

    let private stateView (current: ProviderFailureProjection) =
        createObj
            [ "logicalRun" ==> LogicalRunId.value current.LogicalRunId
              "authorityRoot"
              ==> AuthorityRootUserMessageId.value current.AuthorityRootUserMessageId
              "consecutiveFailureCount" ==> current.Budget.ConsecutiveFailureCount
              "budgetSpec" ==> (current.BudgetSpec |> Option.map specView |> Option.toObj)
              "observedFailures"
              ==> (current.ObservedFailures
                   |> List.map (fun failure ->
                       createObj
                           [ "key" ==> failure.Key
                             "count" ==> failure.Count
                             "reason" ==> failure.Reason ])
                   |> List.toArray)
              "successKeys" ==> List.toArray current.SuccessKeys
              "exhausted" ==> current.Exhausted
              "lastTransitionWasSuccess" ==> current.LastTransitionWasSuccess ]

    let private stateOf (value: obj) : ProviderFailureProjection =
        { LogicalRunId = LogicalRunId.create (unbox<string> value?logicalRun)
          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (unbox<string> value?authorityRoot)
          Budget = { ConsecutiveFailureCount = unbox<int> value?consecutiveFailureCount }
          BudgetSpec =
            if isNull value?budgetSpec then
                None
            else
                Some(specOf value?budgetSpec)
          ObservedFailures =
            (unbox<obj array> value?observedFailures)
            |> Array.map (fun failure ->
                { Key = unbox<string> failure?key
                  Count = unbox<int> failure?count
                  Reason = unbox<string> failure?reason })
            |> Array.toList
          SuccessKeys = (unbox<string array> value?successKeys) |> Array.toList
          Exhausted = unbox<bool> value?exhausted
          LastTransitionWasSuccess = unbox<bool> value?lastTransitionWasSuccess }

    let private rejectionOf name =
        match name with
        | "AlreadyObserved" -> AlreadyObserved
        | "AlreadyExhausted" -> AlreadyExhausted
        | "DifferentRun" -> DifferentRun
        | "NoActiveBudget" -> NoActiveBudget
        | "InvalidTransition" -> InvalidTransition
        | "InvalidBudget" -> InvalidBudget
        | "ConflictingObservation" -> ConflictingObservation
        | _ -> invalidOp ("Unknown provider failure rejection: " + name)

    let private decisionOf (result: obj) =
        if unbox<bool> result?ok then
            Ok(stateOf result?state)
        else
            Error(rejectionOf (unbox<string> result?rejection))

    let forAuthority (logicalRunId: LogicalRunId) (authorityRoot: AuthorityRootUserMessageId) =
        library?recordedForAuthority (LogicalRunId.value logicalRunId, AuthorityRootUserMessageId.value authorityRoot)
        |> stateOf

    /// Apply one failure record.
    let applyFailure
        (identity: FailedProviderAttemptIdentity)
        (spec: ProviderFailureBudgetSpec)
        (consecutiveFailureCount: int)
        (reason: string)
        (current: ProviderFailureProjection)
        : Result<ProviderFailureProjection, ProviderFailureAdvanceRejection> =
        library?recordedApplyFailure (
            FailedProviderAttemptIdentity.view identity,
            specView spec,
            consecutiveFailureCount,
            reason,
            stateView current
        )
        |> decisionOf

    /// Terminal exhausted state.
    let applyExhausted (finalCount: int) (current: ProviderFailureProjection) =
        library?recordedApplyExhausted (finalCount, stateView current) |> decisionOf

    /// Success clears the budget streak.
    let recordSuccess (identity: FailedProviderAttemptIdentity) (current: ProviderFailureProjection) =
        library?recordedSuccess (FailedProviderAttemptIdentity.view identity, stateView current)
        |> decisionOf

    let mayRetry (current: ProviderFailureProjection) : bool =
        library?recordedMayRetry (stateView current)

    let observeFailure
        (identity: FailedProviderAttemptIdentity)
        (reason: string)
        (current: ProviderFailureProjection)
        : Result<ProviderFailureObservation, ProviderFailureAdvanceRejection> =
        let result: obj =
            library?recordedObserveFailure (FailedProviderAttemptIdentity.view identity, reason, stateView current)

        match unbox<bool> result?ok, unbox<string> result?kind, unbox<string> result?outcome with
        | false, _, _ -> Error(rejectionOf (unbox<string> result?rejection))
        | true, "Record", _ ->
            Ok(
                ProviderFailureObservation.Record(
                    specOf result?budgetSpec,
                    unbox<int> result?consecutiveFailureCount,
                    stateOf result?state
                )
            )
        | true, "Replay", "RetryAuthorized" ->
            Ok(ProviderFailureObservation.Replay ProviderFailureReplay.RetryAuthorized)
        | true, "Replay", "RetryExhausted" -> Ok(ProviderFailureObservation.Replay ProviderFailureReplay.RetryExhausted)
        | true, "Replay", "EpisodeSuperseded" ->
            Ok(ProviderFailureObservation.Replay ProviderFailureReplay.EpisodeSuperseded)
        | true, "Replay", name -> invalidOp ("Unknown provider failure replay: " + name)
        | true, name, _ -> invalidOp ("Unknown provider failure observation: " + name)
