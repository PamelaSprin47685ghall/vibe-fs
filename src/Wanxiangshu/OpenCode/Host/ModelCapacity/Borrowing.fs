namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation

type private CapacityStep =
    { SessionId: string
      PhysicalUserMessageId: string
      Fence: Set<string> }

[<RequireQualifiedAccess>]
type private CapacityCreditState =
    | Idle
    | InFlight of CapacityStep
    | Retiring of CapacityStep

type private CapacityCredit<'target> =
    { Credit: CapacityCreditId
      mutable OwnerKey: string
      OwnerSessionId: string
      Provider: string
      mutable OwnerTarget: 'target
      mutable State: CapacityCreditState }

type private CapacityCreditSource =
    { Credit: CapacityCreditId
      LenderSessionId: string
      Distance: int }

/// Decorates the old ledger with lineage-local borrowing and preemptive recall.
/// Borrowing changes who may use a token, never how many real tokens exist.
type internal BorrowingCapacity<'target>
    (ledger: CapacityLedger<'target>, providerOf: 'target -> string, sameTarget: 'target -> 'target -> bool) =
    let gate = obj ()
    /// DSL-cross-callback-proof: physical resource — exact execution-to-lender token ownership
    // DSL-MUTABLE: resource — exact borrowed execution → actual lender token
    let creditSourceByExecution = Dictionary<string, CapacityCreditSource>()
    // DSL-MUTABLE: resource — at most one owned capacity token per execution
    let ownedTokenByExecution = Dictionary<string, CapacityCreditId>()
    // DSL-MUTABLE: resource — decorator metadata for ledger tokens
    let tokens = Dictionary<CapacityCreditId, CapacityCredit<'target>>()
    // DSL-MUTABLE: resource — provider-step admission waiters
    let waiters = CapacityStepDemandQueue<'target>()

    let executionKey sessionId physicalUserMessageId =
        sessionId + "\u001f" + (physicalUserMessageId |> Option.defaultValue "")

    let executionPrefix sessionId = sessionId + "\u001f"

    let executionOwner (key: string) : CapacityExactOwnerSnapshot =
        let separator = key.IndexOf('\u001f')

        { SessionId = key.Substring(0, separator)
          PhysicalUserMessageId = key.Substring(separator + 1)
          Role = None
          Participant = None }

    let tokenStateName =
        function
        | CapacityCreditState.Idle -> "Idle"
        | CapacityCreditState.InFlight _ -> "InFlight"
        | CapacityCreditState.Retiring _ -> "Retiring"

    let currentCreditSource (sessionId: string) : CapacityCreditSource option =
        let sources =
            creditSourceByExecution
            |> Seq.choose (fun (KeyValue(key, source)) ->
                if key.StartsWith(executionPrefix sessionId, StringComparison.Ordinal) then
                    Some source
                else
                    None)
            |> Seq.distinct
            |> Seq.toList

        match sources with
        | [] -> None
        | [ source ] -> Some source
        | _ ->
            invalidOp (sprintf "execution-model-routing: session %s has multiple borrowed capacity sources" sessionId)

    let clearCreditSource key =
        creditSourceByExecution.Remove key |> ignore

    let clearCreditSourcesForToken (tokenId: CapacityCreditId) =
        creditSourceByExecution
        |> Seq.choose (fun (KeyValue(key, source)) -> if source.Credit = tokenId then Some key else None)
        |> Seq.toArray
        |> Array.iter clearCreditSource

    let clearCreditSourcesForSession sessionId =
        creditSourceByExecution.Keys
        |> Seq.filter (fun key -> key.StartsWith(executionPrefix sessionId, StringComparison.Ordinal))
        |> Seq.toArray
        |> Array.iter clearCreditSource

    let rememberCreditSource key borrowerSessionId (token: CapacityCredit<'target>) distance =
        if distance <= 0 || token.OwnerSessionId = borrowerSessionId then
            clearCreditSource key
        else
            creditSourceByExecution.[key] <-
                { Credit = token.Credit
                  LenderSessionId = token.OwnerSessionId
                  Distance = distance }

    let moveCreditSource oldKey newKey =
        match creditSourceByExecution.TryGetValue oldKey with
        | true, source ->
            clearCreditSource oldKey
            creditSourceByExecution.[newKey] <- source
        | false, _ -> ()

    let normalizeProvider (target: 'target) =
        let provider = providerOf target

        if String.IsNullOrWhiteSpace provider then
            invalidOp "execution-model-routing: capacity target has no provider"

        provider.Trim()

    let isRetiring token =
        match token.State with
        | CapacityCreditState.Retiring _ -> true
        | CapacityCreditState.Idle
        | CapacityCreditState.InFlight _ -> false

    let releaseToken (token: CapacityCredit<'target>) =
        clearCreditSourcesForToken token.Credit
        ledger.Release token.Credit |> ignore
        tokens.Remove token.Credit |> ignore

        match ownedTokenByExecution.TryGetValue token.OwnerKey with
        | true, current when current = token.Credit -> ownedTokenByExecution.Remove token.OwnerKey |> ignore
        | _ -> ()

    let retireToken (token: CapacityCredit<'target>) =
        match token.State with
        | CapacityCreditState.Idle -> releaseToken token
        | CapacityCreditState.InFlight step -> token.State <- CapacityCreditState.Retiring step
        | CapacityCreditState.Retiring _ -> ()

    let retireTokenId tokenId =
        match tokens.TryGetValue tokenId with
        | true, token -> retireToken token
        | false, _ -> ()

    let retireExecution key =
        clearCreditSource key

        match ownedTokenByExecution.TryGetValue key with
        | true, tokenId ->
            ownedTokenByExecution.Remove key |> ignore
            retireTokenId tokenId
        | false, _ -> ()

    let creditPair lender (token: CapacityCredit<'target>) =
        match isRetiring token, token.OwnerSessionId = lender with
        | true, _
        | _, false -> None
        | false, true -> Some(token, 1)

    let cheapestPerProvider (provider: string, candidates: (CapacityCredit<'target> * int) seq) =
        let token, _ =
            candidates
            |> Seq.sortBy (fun (token, distance) -> distance, token.Credit)
            |> Seq.head

        provider, token

    let creditTokens (lenderSessionId: string option) =
        match lenderSessionId with
        | None -> Map.empty
        | Some lender ->
            tokens.Values
            |> Seq.choose (creditPair lender)
            |> Seq.groupBy (fun (token, _) -> token.Provider)
            |> Seq.map cheapestPerProvider
            |> Map.ofSeq

    let withoutTokens tokenIds =
        ledger.Entries()
        |> Array.choose (fun (token, target) -> if Set.contains token tokenIds then None else Some target)

    let schedulingView (lenderSessionId: string option) =
        let credits = creditTokens lenderSessionId

        let hidden =
            credits |> Seq.map (fun (KeyValue(_, token)) -> token.Credit) |> Set.ofSeq

        withoutTokens hidden, credits

    let capacityOrdinaryDecision
        (route: 'target array -> 'target option)
        : ('target * CapacityCredit<'target> option) option =
        route (ledger.Snapshot()) |> Option.map (fun target -> target, None)

    let attributedDecision target (credit: CapacityCredit<'target>) route =
        match route (withoutTokens (Set.singleton credit.Credit)) with
        | Some attributable when sameTarget attributable target -> Some(target, Some credit)
        | _ -> capacityOrdinaryDecision route

    let matchingCreditDecision target (credits: Map<string, CapacityCredit<'target>>) route =
        match Map.tryFind (normalizeProvider target) credits with
        | None -> capacityOrdinaryDecision route
        | Some credit when credits.Count = 1 -> Some(target, Some credit)
        | Some credit -> attributedDecision target credit route

    /// A combined borrowed view is only a tentative convenience. When multiple
    /// provider credits were hidden, the chosen target must still be reproducible
    /// with exactly its own provider token hidden; otherwise no single token can
    /// legally pay for that decision.
    let routeDecision (lenderSessionId: string option) route : ('target * CapacityCredit<'target> option) option =
        let borrowedView, credits = schedulingView lenderSessionId
        let borrowed = route borrowedView

        match Map.isEmpty credits, borrowed with
        | true, target -> target |> Option.map (fun selected -> selected, None)
        | false, None -> capacityOrdinaryDecision route
        | false, Some target -> matchingCreditDecision target credits route

    let acquireOwnedToken sessionId physicalUserMessageId target =
        let key = executionKey sessionId physicalUserMessageId

        if ownedTokenByExecution.ContainsKey key then
            invalidOp "execution-model-routing: one execution cannot own two capacity tokens"

        let tokenId = ledger.Acquire target

        let token =
            { Credit = tokenId
              OwnerKey = key
              OwnerSessionId = sessionId
              Provider = normalizeProvider target
              OwnerTarget = target
              State = CapacityCreditState.Idle }

        tokens.[tokenId] <- token
        ownedTokenByExecution.[key] <- tokenId
        token

    let moveOwnedToken oldKey newKey target (token: CapacityCredit<'target>) =
        ownedTokenByExecution.Remove oldKey |> ignore
        token.OwnerKey <- newKey
        token.OwnerTarget <- target
        ownedTokenByExecution.[newKey] <- token.Credit

        match token.State with
        | CapacityCreditState.Idle -> ledger.Retarget(token.Credit, target) |> ignore
        | CapacityCreditState.InFlight step when step.SessionId = token.OwnerSessionId ->
            token.State <- CapacityCreditState.Idle
            ledger.Retarget(token.Credit, target) |> ignore
        | CapacityCreditState.InFlight _ -> ()
        | CapacityCreditState.Retiring _ -> ()

    let finishStep (token: CapacityCredit<'target>) =
        match token.State with
        | CapacityCreditState.Idle -> ()
        | CapacityCreditState.InFlight _ ->
            token.State <- CapacityCreditState.Idle
            ledger.Retarget(token.Credit, token.OwnerTarget) |> ignore
        | CapacityCreditState.Retiring _ -> releaseToken token

    let tryDictionaryValue (table: Dictionary<'key, 'value>) (key: 'key) =
        match table.TryGetValue key with
        | true, value -> Some value
        | false, _ -> None

    let stepBelongsTo sessionId physicalUserMessageId (step: CapacityStep) =
        step.SessionId = sessionId && step.PhysicalUserMessageId = physicalUserMessageId

    let foreignBorrowedStepToken sessionId physicalUserMessageId (token: CapacityCredit<'target>) =
        match token.State with
        | CapacityCreditState.InFlight step
        | CapacityCreditState.Retiring step when not (stepBelongsTo sessionId physicalUserMessageId step) -> Some token
        | _ -> None

    let reconcileFence sessionId physicalUserMessageId fence =
        // Own-step fence progress: finish the owner's current step when the new
        // transform fence strictly supersedes the in-flight fence.
        tokens.Values
        |> Seq.tryFind (fun token ->
            match token.State with
            | CapacityCreditState.InFlight step
            | CapacityCreditState.Retiring step -> stepBelongsTo sessionId physicalUserMessageId step
            | CapacityCreditState.Idle -> false)
        |> Option.iter (fun token ->
            match token.State with
            | CapacityCreditState.InFlight step
            | CapacityCreditState.Retiring step when not (Set.isEmpty (Set.difference fence step.Fence)) ->
                finishStep token
            | _ -> ())

        // Owner transform-entry reclaim: when the owner re-enters messages.transform
        // for this execution, reclaim the owned credit from any foreign InFlight/
        // Retiring step (descendant borrow). Within a turn the owner may stay blocked
        // by that borrow; once the owner's transform fires, the credit returns.
        // Waiting borrowers keep sequence priority via drain (EMR-010).
        executionKey sessionId (Some physicalUserMessageId)
        |> tryDictionaryValue ownedTokenByExecution
        |> Option.bind (tryDictionaryValue tokens)
        |> Option.bind (foreignBorrowedStepToken sessionId physicalUserMessageId)
        |> Option.iter finishStep


    let grant (token: CapacityCredit<'target>) (demand: CapacityStepDemand<'target>) =
        match token.State with
        | CapacityCreditState.Idle ->
            ledger.Retarget(token.Credit, demand.Target) |> ignore

            token.State <-
                CapacityCreditState.InFlight
                    { SessionId = demand.SessionId
                      PhysicalUserMessageId = demand.PhysicalUserMessageId
                      Fence = demand.Fence }

            waiters.Remove demand |> ignore
            AsyncSupport.trySetResult demand.Completion () |> ignore
        | _ -> invalidOp "execution-model-routing: non-idle capacity token was granted"

    let cancelWaiters predicate =
        waiters.Snapshot()
        |> Seq.filter predicate
        |> Seq.toArray
        |> Array.iter (fun demand ->
            waiters.Remove demand |> ignore
            AsyncSupport.trySetCanceled demand.Completion |> ignore)

    let tryOwnedTokenId key =
        match ownedTokenByExecution.TryGetValue key with
        | true, tokenId -> Some tokenId
        | false, _ -> None

    let tryFindToken tokenId =
        match tokens.TryGetValue tokenId with
        | true, token -> Some token
        | false, _ -> None

    let isOwnedGrant (demand: CapacityStepDemand<'target>) (token: CapacityCredit<'target>) =
        token.State = CapacityCreditState.Idle
        && sameTarget token.OwnerTarget demand.Target

    let ownedPair (demand: CapacityStepDemand<'target>) =
        executionKey demand.SessionId (Some demand.PhysicalUserMessageId)
        |> tryOwnedTokenId
        |> Option.bind tryFindToken
        |> Option.filter (isOwnedGrant demand)
        |> Option.map (fun token -> demand.Sequence, token.Credit, demand, token)

    let tryGrantOwned demand =
        ownedPair demand
        |> Option.map (fun (_, _, demand, token) ->
            grant token demand
            true)
        |> Option.defaultValue false

    let tryExecutionSource (demand: CapacityStepDemand<'target>) =
        match
            creditSourceByExecution.TryGetValue(executionKey demand.SessionId (Some demand.PhysicalUserMessageId))
        with
        | true, source -> Some source
        | false, _ -> None

    let isBorrowGrant provider (token: CapacityCredit<'target>) (source: CapacityCreditSource) =
        source.Credit = token.Credit
        && token.OwnerSessionId = source.LenderSessionId
        && token.Provider = provider

    let borrowPair (demand: CapacityStepDemand<'target>) (token: CapacityCredit<'target>) =
        let provider = normalizeProvider demand.Target

        match token.State, tryExecutionSource demand with
        | CapacityCreditState.Idle, Some source when isBorrowGrant provider token source ->
            Some(source.Distance, demand.Sequence, token.Credit, demand, token)
        | _ -> None

    let tryGrantBorrowed demand =
        tokens.Values
        |> Seq.choose (borrowPair demand)
        |> Seq.sortBy (fun (distance, _, token, _, _) -> distance, token)
        |> Seq.tryHead
        |> Option.map (fun (_, _, _, demand, token) ->
            grant token demand
            true)
        |> Option.defaultValue false

    let demandOwnsToken (demand: CapacityStepDemand<'target>) =
        ownedTokenByExecution.ContainsKey(executionKey demand.SessionId (Some demand.PhysicalUserMessageId))

    let tryGrantOrdinary demand =
        if demandOwnsToken demand || not (demand.TryOrdinary(ledger.Snapshot())) then
            false
        else
            acquireOwnedToken demand.SessionId (Some demand.PhysicalUserMessageId) demand.Target
            |> fun token -> grant token demand

            true

    let tryGrantDemand demand =
        tryGrantOwned demand || tryGrantBorrowed demand || tryGrantOrdinary demand

    let rec drain () =
        let granted =
            waiters.Snapshot()
            |> Seq.sortBy _.Sequence
            |> Seq.tryFind tryGrantDemand
            |> Option.isSome

        if granted then
            drain ()

    let acquireForRoute sessionId physicalUserMessageId target (credit: CapacityCredit<'target> option) =
        match credit with
        | Some _ -> ()
        | None -> acquireOwnedToken sessionId physicalUserMessageId target |> ignore

    let recordRoutedCredit sessionId key lenderSessionId (credit: CapacityCredit<'target> option) =
        match credit, lenderSessionId with
        | None, _ -> clearCreditSource key
        | Some token, Some lender when lender = token.OwnerSessionId -> rememberCreditSource key sessionId token 1
        | Some _, _ -> invalidOp "execution-model-routing: routed credit is not legal for requester"

    let applyRoutedToken
        sessionId
        oldKey
        newKey
        newPhysicalUserMessageId
        target
        (credit: CapacityCredit<'target> option)
        =
        match oldKey, credit with
        | Some previousKey, Some token when token.OwnerKey = previousKey ->
            moveOwnedToken previousKey newKey target token
        | _ ->
            acquireForRoute sessionId (Some newPhysicalUserMessageId) target credit
            oldKey |> Option.iter retireExecution

    let commitRoutedTarget sessionId oldKey newKey newPhysicalUserMessageId target lenderSessionId credit =
        applyRoutedToken sessionId oldKey newKey newPhysicalUserMessageId target credit
        oldKey |> Option.iter clearCreditSource
        recordRoutedCredit sessionId newKey lenderSessionId credit
        target

    let ensureReservationToken sessionId target (credit: CapacityCredit<'target> option) =
        match credit with
        | Some _ -> ()
        | None -> acquireOwnedToken sessionId None target |> ignore

    let recordReservationCredit sessionId key lenderSessionId (credit: CapacityCredit<'target> option) =
        match credit, lenderSessionId with
        | None, _ -> clearCreditSource key
        | Some token, Some lender when lender = token.OwnerSessionId -> rememberCreditSource key sessionId token 1
        | Some _, _ -> invalidOp "execution-model-routing: reserved credit is not legal for requester"

    let adoptOwnedToken oldKey newKey target tokenId =
        match tokens.TryGetValue tokenId with
        | true, token -> moveOwnedToken oldKey newKey target token
        | false, _ -> ownedTokenByExecution.Remove oldKey |> ignore

    member _.RouteFresh
        (
            sessionId: string,
            oldPhysicalUserMessageId: string option,
            newPhysicalUserMessageId: string,
            lenderSessionId: string option,
            route: 'target array -> 'target option
        ) =
        lock gate (fun () ->
            let oldKey =
                oldPhysicalUserMessageId
                |> Option.map (fun physical -> executionKey sessionId (Some physical))

            let newKey = executionKey sessionId (Some newPhysicalUserMessageId)

            match routeDecision lenderSessionId route with
            | None ->
                oldKey |> Option.iter retireExecution
                None
            | Some(target, credit) ->
                commitRoutedTarget sessionId oldKey newKey newPhysicalUserMessageId target lenderSessionId credit
                |> Some)

    member _.ReserveFresh(sessionId: string, lenderSessionId: string option, route: 'target array -> 'target option) =
        lock gate (fun () ->
            match routeDecision lenderSessionId route with
            | None -> None
            | Some(target, credit) ->
                ensureReservationToken sessionId target credit

                let key = executionKey sessionId None
                recordReservationCredit sessionId key lenderSessionId credit
                Some target)

    member _.AdoptReservation(sessionId: string, physicalUserMessageId: string, target: 'target) =
        lock gate (fun () ->
            let oldKey = executionKey sessionId None
            let newKey = executionKey sessionId (Some physicalUserMessageId)

            match ownedTokenByExecution.TryGetValue oldKey with
            | true, tokenId -> adoptOwnedToken oldKey newKey target tokenId
            | false, _ -> ()

            moveCreditSource oldKey newKey)

    member _.ReleaseSession(sessionId: string) =
        lock gate (fun () ->
            let prefix = sessionId + "\u001f"

            let existed =
                waiters.Snapshot() |> Array.exists (fun demand -> demand.SessionId = sessionId)
                || (ownedTokenByExecution.Keys
                    |> Seq.exists (fun key -> key.StartsWith(prefix, StringComparison.Ordinal)))
                || (creditSourceByExecution.Keys
                    |> Seq.exists (fun key -> key.StartsWith(prefix, StringComparison.Ordinal)))

            cancelWaiters (fun demand -> demand.SessionId = sessionId)
            clearCreditSourcesForSession sessionId

            ownedTokenByExecution.Keys
            |> Seq.filter (fun key -> key.StartsWith(prefix, StringComparison.Ordinal))
            |> Seq.toArray
            |> Array.iter retireExecution

            drain ()

            if existed then
                CapacityTransitionOutcome.Applied
            else
                CapacityTransitionOutcome.AlreadyApplied)

    member _.ReleasePhysical(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () ->
            let key = executionKey sessionId (Some physicalUserMessageId)

            let existed =
                ownedTokenByExecution.ContainsKey key
                || creditSourceByExecution.ContainsKey key
                || (waiters.Snapshot()
                    |> Array.exists (fun demand ->
                        demand.SessionId = sessionId
                        && demand.PhysicalUserMessageId = physicalUserMessageId))

            cancelWaiters (fun demand ->
                demand.SessionId = sessionId
                && demand.PhysicalUserMessageId = physicalUserMessageId)

            retireExecution key
            drain ()

            if existed then
                CapacityTransitionOutcome.Applied
            else
                CapacityTransitionOutcome.AlreadyApplied)

    member _.EnterStep
        (
            sessionId: string,
            physicalUserMessageId: string,
            target: 'target,
            fence: Set<string>,
            tryOrdinary: 'target array -> bool
        ) : Task =
        lock gate (fun () ->
            reconcileFence sessionId physicalUserMessageId fence

            let completion =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            let demand =
                { Sequence = waiters.NextSequence()
                  SessionId = sessionId
                  PhysicalUserMessageId = physicalUserMessageId
                  Target = target
                  Fence = fence
                  TryOrdinary = tryOrdinary
                  Completion = completion }

            if waiters.TryAdd demand then
                drain ()
                completion.Task :> Task
            else
                completion.SetException(
                    InvalidOperationException "execution-model-routing: provider-step capacity queue full"
                )

                completion.Task :> Task)

    member _.EndStep(sessionId: string, physicalUserMessageId: string, providerRun: string) =
        lock gate (fun () ->
            tokens.Values
            |> Seq.tryFind (fun token ->
                match token.State with
                | CapacityCreditState.InFlight step
                | CapacityCreditState.Retiring step ->
                    step.SessionId = sessionId
                    && step.PhysicalUserMessageId = physicalUserMessageId
                    && not (Set.contains providerRun step.Fence)
                | CapacityCreditState.Idle -> false)
            |> Option.iter finishStep

            drain ())

    member _.SuppressStep(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () ->
            tokens.Values
            |> Seq.tryFind (fun token ->
                match token.State with
                | CapacityCreditState.InFlight step
                | CapacityCreditState.Retiring step ->
                    step.SessionId = sessionId && step.PhysicalUserMessageId = physicalUserMessageId
                | CapacityCreditState.Idle -> false)
            |> Option.iter finishStep

            drain ())

    member internal _.ExactCredit(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () ->
            let key = executionKey sessionId (Some physicalUserMessageId)

            match ownedTokenByExecution.TryGetValue key, creditSourceByExecution.TryGetValue key with
            | (true, credit), _ -> credit
            | (false, _), (true, source) -> source.Credit
            | _ ->
                invalidOp (
                    sprintf
                        "execution-model-routing: physical execution %s/%s has no exact capacity credit"
                        sessionId
                        physicalUserMessageId
                ))

    member _.InvariantSnapshot() : BorrowingCapacitySnapshot<'target> =
        lock gate (fun () ->
            let ledgerEntries =
                ledger.Entries()
                |> Array.map (fun (credit, target) ->
                    { Credit = CapacityCreditId.value credit
                      Target = target })
                |> Array.sortBy _.Credit

            let targetByCredit =
                ledgerEntries
                |> Array.map (fun entry -> entry.Credit, entry.Target)
                |> Map.ofArray

            let tokenSnapshots =
                tokens.Values
                |> Seq.map (fun token ->
                    { Credit = CapacityCreditId.value token.Credit
                      State = tokenStateName token.State
                      Owner = executionOwner token.OwnerKey
                      Target = targetByCredit.[CapacityCreditId.value token.Credit] })
                |> Seq.sortBy _.Credit
                |> Seq.toArray

            let ownedCustodies =
                ownedTokenByExecution
                |> Seq.map (fun (KeyValue(key, credit)) ->
                    { Credit = CapacityCreditId.value credit
                      Owner = executionOwner key })

            let borrowedCustodies =
                creditSourceByExecution
                |> Seq.map (fun (KeyValue(key, source)) ->
                    { Credit = CapacityCreditId.value source.Credit
                      Owner = executionOwner key })

            let custodies =
                Seq.append ownedCustodies borrowedCustodies
                |> Seq.distinctBy (fun custody ->
                    custody.Credit, custody.Owner.SessionId, custody.Owner.PhysicalUserMessageId)
                |> Seq.sortBy (fun custody -> custody.Owner.SessionId, custody.Owner.PhysicalUserMessageId)
                |> Seq.toArray

            let waiterSnapshots =
                waiters.Snapshot()
                |> Array.map (fun waiter ->
                    { Owner =
                        { SessionId = waiter.SessionId
                          PhysicalUserMessageId = waiter.PhysicalUserMessageId
                          Role = None
                          Participant = None }
                      Sequence = waiter.Sequence
                      Kind = "ProviderStep" })
                |> Array.sortBy _.Sequence

            let lineage: CapacityLineageSnapshot array = Array.empty

            { LedgerEntries = ledgerEntries
              Tokens = tokenSnapshots
              Custodies = custodies
              Waiters = waiterSnapshots
              Lineage = lineage
              IdleCount =
                tokenSnapshots
                |> Array.sumBy (fun token -> if token.State = "Idle" then 1 else 0)
              InFlightCount =
                tokenSnapshots
                |> Array.sumBy (fun token -> if token.State = "InFlight" then 1 else 0)
              RetiringCount =
                tokenSnapshots
                |> Array.sumBy (fun token -> if token.State = "Retiring" then 1 else 0) })

    member _.Snapshot() = lock gate (fun () -> ledger.Snapshot())

    member _.Fail(error: exn) =
        lock gate (fun () ->
            let pending = waiters.Snapshot()
            waiters.Clear()

            pending
            |> Array.iter (fun demand ->
                try
                    demand.Completion.SetException(error)
                with _ ->
                    ()))
