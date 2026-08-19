namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation

/// The old capacity truth: one opaque token = one occurrence in scheduler `running`.
/// It knows nothing about sessions, ancestry, borrowing, provider steps, or limits.
type CapacityLedger<'target>() =
    let gate = obj ()
    let entries = Dictionary<int64, 'target>()
    // DSL-MUTABLE: resource — monotonic opaque capacity-token identity
    let mutable nextToken = 0L

    member _.Acquire(target: 'target) =
        lock gate (fun () ->
            nextToken <- nextToken + 1L
            entries.[nextToken] <- target
            nextToken)

    member _.Retarget(token: int64, target: 'target) =
        lock gate (fun () ->
            if entries.ContainsKey token then
                entries.[token] <- target)

    member _.Release(token: int64) = lock gate (fun () -> entries.Remove token)

    member _.Entries() =
        lock gate (fun () -> entries |> Seq.map (fun (KeyValue(token, target)) -> token, target) |> Seq.toArray)

    member this.Snapshot() = this.Entries() |> Array.map snd

type private CapacityStep =
    { SessionId: string
      PhysicalUserMessageId: string
      Fence: Set<string> }

[<RequireQualifiedAccess>]
type private CapacityTokenState =
    | Idle
    | InFlight of CapacityStep
    | Retiring of CapacityStep

type private CapacityToken<'target> =
    { Token: int64
      mutable OwnerKey: string
      OwnerSessionId: string
      Provider: string
      mutable OwnerTarget: 'target
      mutable State: CapacityTokenState }

type private CapacityStepDemand<'target> =
    { Sequence: int64
      SessionId: string
      PhysicalUserMessageId: string
      Target: 'target
      Fence: Set<string>
      TryOrdinary: 'target array -> bool
      Completion: TaskCompletionSource<unit> }

/// Decorates the old ledger with lineage-local borrowing and preemptive recall.
/// Borrowing changes who may use a token, never how many real tokens exist.
type BorrowingCapacity<'target>
    (
        ledger: CapacityLedger<'target>,
        providerOf: 'target -> string,
        sameTarget: 'target -> 'target -> bool
    ) =
    let gate = obj ()
    // DSL-MUTABLE: resource — capacity-only session lineage
    let parents = Dictionary<string, string>()
    // DSL-MUTABLE: resource — at most one owned capacity token per execution
    let ownedTokenByExecution = Dictionary<string, int64>()
    // DSL-MUTABLE: resource — decorator metadata for ledger tokens
    let tokens = Dictionary<int64, CapacityToken<'target>>()
    // DSL-MUTABLE: resource — provider-step admission waiters
    let waiters = ResizeArray<CapacityStepDemand<'target>>()
    let mutable nextDemand = 0L

    let executionKey sessionId physicalUserMessageId =
        sessionId + "\u001f" + (physicalUserMessageId |> Option.defaultValue "")

    let normalizeProvider (target: 'target) =
        let provider = providerOf target

        if String.IsNullOrWhiteSpace provider then
            invalidOp "execution-model-routing: capacity target has no provider"

        provider.Trim()

    let nextAncestor current distance visited =
        match parents.TryGetValue current with
        | true, parent -> Some(parent, distance + 1, Set.add current visited)
        | false, _ -> None

    let ancestorDistance ancestor descendant =
        let rec loop current distance visited =
            if current = ancestor then
                Some distance
            elif Set.contains current visited then
                None
            else
                nextAncestor current distance visited
                |> Option.bind (fun (parent, nextDistance, nextVisited) -> loop parent nextDistance nextVisited)

        loop descendant 0 Set.empty

    let isAncestor ancestor descendant = ancestorDistance ancestor descendant |> Option.isSome

    let isRetiring token =
        match token.State with
        | CapacityTokenState.Retiring _ -> true
        | CapacityTokenState.Idle
        | CapacityTokenState.InFlight _ -> false

    let releaseToken (token: CapacityToken<'target>) =
        ledger.Release token.Token |> ignore
        tokens.Remove token.Token |> ignore

        match ownedTokenByExecution.TryGetValue token.OwnerKey with
        | true, current when current = token.Token -> ownedTokenByExecution.Remove token.OwnerKey |> ignore
        | _ -> ()

    let retireToken (token: CapacityToken<'target>) =
        match token.State with
        | CapacityTokenState.Idle -> releaseToken token
        | CapacityTokenState.InFlight step -> token.State <- CapacityTokenState.Retiring step
        | CapacityTokenState.Retiring _ -> ()

    let retireTokenId tokenId =
        match tokens.TryGetValue tokenId with
        | true, token -> retireToken token
        | false, _ -> ()

    let retireExecution key =
        match ownedTokenByExecution.TryGetValue key with
        | true, tokenId ->
            ownedTokenByExecution.Remove key |> ignore
            retireTokenId tokenId
        | false, _ -> ()

    let creditTokens requester =
        tokens.Values
        |> Seq.choose (fun token ->
            if isRetiring token || not (isAncestor token.OwnerSessionId requester) then
                None
            else
                ancestorDistance token.OwnerSessionId requester
                |> Option.map (fun distance -> token, distance))
        |> Seq.groupBy (fun (token, _) -> token.Provider)
        |> Seq.map (fun (provider, candidates) ->
            let token, _ = candidates |> Seq.sortBy (fun (token, distance) -> distance, token.Token) |> Seq.head
            provider, token)
        |> Map.ofSeq

    let withoutTokens tokenIds =
        ledger.Entries()
        |> Array.choose (fun (token, target) -> if Set.contains token tokenIds then None else Some target)

    let schedulingView requester =
        let credits = creditTokens requester
        let hidden = credits |> Seq.map (fun (KeyValue(_, token)) -> token.Token) |> Set.ofSeq
        withoutTokens hidden, credits

    let ordinaryDecision route =
        route (ledger.Snapshot()) |> Option.map (fun target -> target, None)

    let attributedDecision target credit route =
        match route (withoutTokens (Set.singleton credit.Token)) with
        | Some attributable when sameTarget attributable target -> Some(target, Some credit)
        | _ -> ordinaryDecision route

    let matchingCreditDecision target credits route =
        match Map.tryFind (normalizeProvider target) credits with
        | None -> ordinaryDecision route
        | Some credit when credits.Count = 1 -> Some(target, Some credit)
        | Some credit -> attributedDecision target credit route

    /// A combined borrowed view is only a tentative convenience. When multiple
    /// provider credits were hidden, the chosen target must still be reproducible
    /// with exactly its own provider token hidden; otherwise no single token can
    /// legally pay for that decision.
    let routeDecision requester route =
        let borrowedView, credits = schedulingView requester
        let borrowed = route borrowedView

        match Map.isEmpty credits, borrowed with
        | true, target -> target |> Option.map (fun selected -> selected, None)
        | false, None -> ordinaryDecision route
        | false, Some target -> matchingCreditDecision target credits route

    let acquireOwnedToken sessionId physicalUserMessageId target =
        let key = executionKey sessionId physicalUserMessageId

        if ownedTokenByExecution.ContainsKey key then
            invalidOp "execution-model-routing: one execution cannot own two capacity tokens"

        let tokenId = ledger.Acquire target
        let token =
            { Token = tokenId
              OwnerKey = key
              OwnerSessionId = sessionId
              Provider = normalizeProvider target
              OwnerTarget = target
              State = CapacityTokenState.Idle }

        tokens.[tokenId] <- token
        ownedTokenByExecution.[key] <- tokenId
        token

    let moveOwnedToken oldKey newKey target (token: CapacityToken<'target>) =
        ownedTokenByExecution.Remove oldKey |> ignore
        token.OwnerKey <- newKey
        token.OwnerTarget <- target
        ownedTokenByExecution.[newKey] <- token.Token

        match token.State with
        | CapacityTokenState.Idle -> ledger.Retarget(token.Token, target)
        | CapacityTokenState.InFlight _
        | CapacityTokenState.Retiring _ -> ()

    let finishStep (token: CapacityToken<'target>) =
        match token.State with
        | CapacityTokenState.Idle -> ()
        | CapacityTokenState.InFlight _ ->
            token.State <- CapacityTokenState.Idle
            ledger.Retarget(token.Token, token.OwnerTarget)
        | CapacityTokenState.Retiring _ -> releaseToken token

    let reconcileFence sessionId physicalUserMessageId fence =
        tokens.Values
        |> Seq.tryFind (fun token ->
            match token.State with
            | CapacityTokenState.InFlight step
            | CapacityTokenState.Retiring step ->
                step.SessionId = sessionId && step.PhysicalUserMessageId = physicalUserMessageId
            | CapacityTokenState.Idle -> false)
        |> Option.iter (fun token ->
            match token.State with
            | CapacityTokenState.InFlight step
            | CapacityTokenState.Retiring step when not (Set.isEmpty (Set.difference fence step.Fence)) ->
                finishStep token
            | _ -> ())

    let grant (token: CapacityToken<'target>) (demand: CapacityStepDemand<'target>) =
        match token.State with
        | CapacityTokenState.Idle ->
            ledger.Retarget(token.Token, demand.Target)
            token.State <-
                CapacityTokenState.InFlight
                    { SessionId = demand.SessionId
                      PhysicalUserMessageId = demand.PhysicalUserMessageId
                      Fence = demand.Fence }

            waiters.Remove demand |> ignore
            AsyncSupport.trySetResult demand.Completion () |> ignore
        | _ -> invalidOp "execution-model-routing: non-idle capacity token was granted"

    let cancelWaiters predicate =
        waiters
        |> Seq.filter predicate
        |> Seq.toArray
        |> Array.iter (fun demand ->
            waiters.Remove demand |> ignore
            AsyncSupport.trySetCanceled demand.Completion |> ignore)

    let borrowPair (demand: CapacityStepDemand<'target>) (token: CapacityToken<'target>) =
        let provider = normalizeProvider demand.Target

        match token.State, ancestorDistance token.OwnerSessionId demand.SessionId with
        | CapacityTokenState.Idle, Some distance when token.Provider = provider ->
            Some(distance, demand.Sequence, token.Token, demand, token)
        | _ -> None

    let idleBorrowPairs () =
        waiters
        |> Seq.collect (fun demand -> tokens.Values |> Seq.choose (borrowPair demand))
        |> Seq.toList

    let tryGrantBorrowed () =
        match idleBorrowPairs () |> List.sortBy (fun (distance, sequence, token, _, _) -> distance, sequence, token) with
        | [] -> false
        | (_, _, _, demand, token) :: _ ->
            grant token demand
            true

    let demandOwnsToken (demand: CapacityStepDemand<'target>) =
        ownedTokenByExecution.ContainsKey(executionKey demand.SessionId (Some demand.PhysicalUserMessageId))

    let tryGrantOrdinary () =
        waiters
        |> Seq.sortBy _.Sequence
        |> Seq.tryFind (fun demand -> not (demandOwnsToken demand) && demand.TryOrdinary(ledger.Snapshot()))
        |> Option.map (fun demand ->
            acquireOwnedToken demand.SessionId (Some demand.PhysicalUserMessageId) demand.Target
            |> fun token -> grant token demand

            true)
        |> Option.defaultValue false

    let rec drain () =
        if tryGrantBorrowed () || tryGrantOrdinary () then
            drain ()

    let acquireForRoute sessionId physicalUserMessageId target credit =
        match credit with
        | Some _ -> ()
        | None -> acquireOwnedToken sessionId physicalUserMessageId target |> ignore

    let commitRoutedTarget sessionId oldKey newKey newPhysicalUserMessageId target credit =
        match oldKey, credit with
        | Some previousKey, Some token when token.OwnerKey = previousKey ->
            moveOwnedToken previousKey newKey target token
        | _ ->
            acquireForRoute sessionId (Some newPhysicalUserMessageId) target credit
            oldKey |> Option.iter retireExecution

        target

    let adoptOwnedToken oldKey newKey target tokenId =
        match tokens.TryGetValue tokenId with
        | true, token -> moveOwnedToken oldKey newKey target token
        | false, _ -> ownedTokenByExecution.Remove oldKey |> ignore

    member _.BindChild(parentSessionId: string, childSessionId: string) =
        lock gate (fun () ->
            if String.IsNullOrWhiteSpace parentSessionId || String.IsNullOrWhiteSpace childSessionId then
                invalidArg "sessionId" "execution-model-routing: capacity lineage requires non-empty session ids"

            let parent = parentSessionId.Trim()
            let child = childSessionId.Trim()

            if parent = child || isAncestor child parent then
                invalidOp "execution-model-routing: capacity lineage cycle"

            match parents.TryGetValue child with
            | true, existing when existing <> parent ->
                invalidOp (sprintf "execution-model-routing: capacity child %s changed parent" child)
            | _ -> parents.[child] <- parent)

    member _.DropLineage(sessionId: string) =
        lock gate (fun () ->
            parents.Remove sessionId |> ignore

            parents.Keys
            |> Seq.filter (fun child -> parents.[child] = sessionId)
            |> Seq.toArray
            |> Array.iter (fun child -> parents.Remove child |> ignore))

    member _.RouteFresh
        (
            sessionId: string,
            oldPhysicalUserMessageId: string option,
            newPhysicalUserMessageId: string,
            route: 'target array -> 'target option
        ) =
        lock gate (fun () ->
            let oldKey = oldPhysicalUserMessageId |> Option.map (fun physical -> executionKey sessionId (Some physical))
            let newKey = executionKey sessionId (Some newPhysicalUserMessageId)

            match routeDecision sessionId route with
            | None ->
                oldKey |> Option.iter retireExecution
                None
            | Some(target, credit) ->
                commitRoutedTarget sessionId oldKey newKey newPhysicalUserMessageId target credit |> Some)

    member _.ReserveFresh(sessionId: string, route: 'target array -> 'target option) =
        lock gate (fun () ->
            match routeDecision sessionId route with
            | None -> None
            | Some(target, credit) ->
                if credit.IsNone then acquireOwnedToken sessionId None target |> ignore
                Some target)

    member _.AdoptReservation(sessionId: string, physicalUserMessageId: string, target: 'target) =
        lock gate (fun () ->
            let oldKey = executionKey sessionId None
            let newKey = executionKey sessionId (Some physicalUserMessageId)

            match ownedTokenByExecution.TryGetValue oldKey with
            | true, tokenId -> adoptOwnedToken oldKey newKey target tokenId
            | false, _ -> ())

    member _.ReleaseSession(sessionId: string) =
        lock gate (fun () ->
            cancelWaiters (fun demand -> demand.SessionId = sessionId)

            ownedTokenByExecution.Keys
            |> Seq.filter (fun key -> key.StartsWith(sessionId + "\u001f", StringComparison.Ordinal))
            |> Seq.toArray
            |> Array.iter retireExecution

            drain ())

    member _.ReleasePhysical(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () ->
            cancelWaiters (fun demand ->
                demand.SessionId = sessionId && demand.PhysicalUserMessageId = physicalUserMessageId)

            retireExecution (executionKey sessionId (Some physicalUserMessageId))
            drain ())

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
            nextDemand <- nextDemand + 1L

            let completion = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            let demand =
                { Sequence = nextDemand
                  SessionId = sessionId
                  PhysicalUserMessageId = physicalUserMessageId
                  Target = target
                  Fence = fence
                  TryOrdinary = tryOrdinary
                  Completion = completion }

            waiters.Add demand
            drain ()
            completion.Task :> Task)

    member _.EndStep(sessionId: string, physicalUserMessageId: string, providerRun: string) =
        lock gate (fun () ->
            tokens.Values
            |> Seq.tryFind (fun token ->
                match token.State with
                | CapacityTokenState.InFlight step
                | CapacityTokenState.Retiring step ->
                    step.SessionId = sessionId
                    && step.PhysicalUserMessageId = physicalUserMessageId
                    && not (Set.contains providerRun step.Fence)
                | CapacityTokenState.Idle -> false)
            |> Option.iter finishStep

            drain ())

    member _.SuppressStep(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () ->
            tokens.Values
            |> Seq.tryFind (fun token ->
                match token.State with
                | CapacityTokenState.InFlight step
                | CapacityTokenState.Retiring step ->
                    step.SessionId = sessionId && step.PhysicalUserMessageId = physicalUserMessageId
                | CapacityTokenState.Idle -> false)
            |> Option.iter finishStep

            drain ())

    member _.Snapshot() = lock gate (fun () -> ledger.Snapshot())

    member _.Fail(error: exn) =
        lock gate (fun () ->
            let pending = waiters |> Seq.toArray
            waiters.Clear()

            pending
            |> Array.iter (fun demand ->
                try
                    demand.Completion.SetException(error)
                with _ ->
                    ()))
