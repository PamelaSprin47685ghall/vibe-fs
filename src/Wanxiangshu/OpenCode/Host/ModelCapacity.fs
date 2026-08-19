namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation

type CapacityLedger<'target>() =
    let gate = obj ()
    let entries = Dictionary<int64, 'target>()
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

type private CapacityToken<'target> =
    { Token: int64
      mutable OwnerKey: string
      mutable OwnerSessionId: string
      Provider: string
      mutable OwnerTarget: 'target
      mutable InFlight: CapacityStep option
      mutable Retiring: bool }

type private CapacityStepDemand<'target> =
    { Sequence: int64
      SessionId: string
      PhysicalUserMessageId: string
      Target: 'target
      Fence: Set<string>
      TryOrdinary: 'target array -> bool
      Completion: TaskCompletionSource<unit> }

type BorrowingCapacity<'target>
    (
        ledger: CapacityLedger<'target>,
        providerOf: 'target -> string,
        sameTarget: 'target -> 'target -> bool
    ) =
    let gate = obj ()
    let parents = Dictionary<string, string>()
    let ownedTokenByExecution = Dictionary<string, int64>()
    let tokens = Dictionary<int64, CapacityToken<'target>>()
    let waiters = ResizeArray<CapacityStepDemand<'target>>()
    let mutable nextDemand = 0L

    let executionKey sessionId physicalUserMessageId =
        sessionId + "\u001f" + (physicalUserMessageId |> Option.defaultValue "")

    let normalizeProvider (target: 'target) =
        let provider = providerOf target

        if String.IsNullOrWhiteSpace provider then
            invalidOp "execution-model-routing: capacity target has no provider"

        provider.Trim()

    let ancestorDistance ancestor descendant =
        let rec loop current distance visited =
            if current = ancestor then
                Some distance
            elif Set.contains current visited then
                None
            else
                match parents.TryGetValue current with
                | true, parent -> loop parent (distance + 1) (Set.add current visited)
                | false, _ -> None

        loop descendant 0 Set.empty

    let isAncestor ancestor descendant = ancestorDistance ancestor descendant |> Option.isSome

    let releaseToken (token: CapacityToken<'target>) =
        ledger.Release token.Token |> ignore
        tokens.Remove token.Token |> ignore

        match ownedTokenByExecution.TryGetValue token.OwnerKey with
        | true, current when current = token.Token -> ownedTokenByExecution.Remove token.OwnerKey |> ignore
        | _ -> ()

    let retireToken (token: CapacityToken<'target>) =
        token.Retiring <- true

        match token.InFlight with
        | None -> releaseToken token
        | Some _ -> ()

    let retireExecution key =
        match ownedTokenByExecution.TryGetValue key with
        | true, tokenId ->
            ownedTokenByExecution.Remove key |> ignore

            match tokens.TryGetValue tokenId with
            | true, token -> retireToken token
            | false, _ -> ()
        | false, _ -> ()

    let creditTokens requester =
        tokens.Values
        |> Seq.choose (fun token ->
            if token.Retiring || not (isAncestor token.OwnerSessionId requester) then
                None
            else
                ancestorDistance token.OwnerSessionId requester
                |> Option.map (fun distance -> token, distance))
        |> Seq.groupBy (fun (token, _) -> token.Provider)
        |> Seq.map (fun (provider, candidates) ->
            let token, _ =
                candidates
                |> Seq.sortBy (fun (token, distance) -> distance, token.Token)
                |> Seq.head

            provider, token)
        |> Map.ofSeq

    let schedulingView requester =
        let credits = creditTokens requester
        let hidden = credits |> Seq.map (fun (KeyValue(_, token)) -> token.Token) |> Set.ofSeq

        ledger.Entries()
        |> Array.choose (fun (token, target) -> if Set.contains token hidden then None else Some target), credits

    let withoutToken tokenId =
        ledger.Entries()
        |> Array.choose (fun (token, target) -> if token = tokenId then None else Some target)

    let ordinaryDecision route =
        route (ledger.Snapshot()) |> Option.map (fun target -> target, None)

    let routeDecision requester route =
        let borrowedView, credits = schedulingView requester

        if Map.isEmpty credits then
            route borrowedView |> Option.map (fun target -> target, None)
        else
            match route borrowedView with
            | None -> ordinaryDecision route
            | Some target ->
                match Map.tryFind (normalizeProvider target) credits with
                | None -> ordinaryDecision route
                | Some credit when credits.Count = 1 -> Some(target, Some credit)
                | Some credit ->
                    match route (withoutToken credit.Token) with
                    | Some attributable when sameTarget attributable target -> Some(target, Some credit)
                    | _ -> ordinaryDecision route

    let acquireOwnedToken sessionId physicalUserMessageId target =
        let tokenId = ledger.Acquire target
        let key = executionKey sessionId physicalUserMessageId
        let token =
            { Token = tokenId
              OwnerKey = key
              OwnerSessionId = sessionId
              Provider = normalizeProvider target
              OwnerTarget = target
              InFlight = None
              Retiring = false }

        tokens.[tokenId] <- token
        ownedTokenByExecution.[key] <- tokenId
        token

    let moveOwnedToken oldKey newKey target (token: CapacityToken<'target>) =
        ownedTokenByExecution.Remove oldKey |> ignore
        token.OwnerKey <- newKey
        token.OwnerTarget <- target
        token.Retiring <- false
        ownedTokenByExecution.[newKey] <- token.Token

        match token.InFlight with
        | None -> ledger.Retarget(token.Token, target)
        | Some _ -> ()

    let finishStep (token: CapacityToken<'target>) =
        token.InFlight <- None

        if token.Retiring then
            releaseToken token
        else
            ledger.Retarget(token.Token, token.OwnerTarget)

    let reconcileFence sessionId physicalUserMessageId fence =
        tokens.Values
        |> Seq.tryFind (fun token ->
            match token.InFlight with
            | Some step -> step.SessionId = sessionId && step.PhysicalUserMessageId = physicalUserMessageId
            | None -> false)
        |> Option.iter (fun token ->
            match token.InFlight with
            | Some step when not (Set.isEmpty (Set.difference fence step.Fence)) -> finishStep token
            | _ -> ())

    let grant token (demand: CapacityStepDemand<'target>) =
        ledger.Retarget(token.Token, demand.Target)
        token.InFlight <-
            Some
                { SessionId = demand.SessionId
                  PhysicalUserMessageId = demand.PhysicalUserMessageId
                  Fence = demand.Fence }

        waiters.Remove demand |> ignore
        AsyncSupport.trySetResult demand.Completion () |> ignore

    let cancelWaiters predicate =
        waiters
        |> Seq.filter predicate
        |> Seq.toArray
        |> Array.iter (fun demand ->
            waiters.Remove demand |> ignore
            AsyncSupport.trySetCanceled demand.Completion |> ignore)

    let idleBorrowPairs () =
        [ for demand in waiters do
              let provider = normalizeProvider demand.Target

              for token in tokens.Values do
                  match token.InFlight, token.Retiring, ancestorDistance token.OwnerSessionId demand.SessionId with
                  | None, false, Some distance when token.Provider = provider ->
                      yield distance, demand.Sequence, token.Token, demand, token
                  | _ -> () ]

    let tryGrantBorrowed () =
        match idleBorrowPairs () |> List.sortBy (fun (distance, sequence, token, _, _) -> distance, sequence, token) with
        | [] -> false
        | (_, _, _, demand, token) :: _ ->
            grant token demand
            true

    let tryGrantOrdinary () =
        waiters
        |> Seq.sortBy (fun demand -> demand.Sequence)
        |> Seq.tryFind (fun demand -> demand.TryOrdinary(ledger.Snapshot()))
        |> Option.map (fun demand ->
            let key = executionKey demand.SessionId (Some demand.PhysicalUserMessageId)
            let token = acquireOwnedToken demand.SessionId (Some demand.PhysicalUserMessageId) demand.Target
            ownedTokenByExecution.[key] <- token.Token
            grant token demand
            true)
        |> Option.defaultValue false

    let rec drain () =
        if tryGrantBorrowed () || tryGrantOrdinary () then
            drain ()

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
                match oldKey, credit with
                | Some previousKey, Some token when token.OwnerKey = previousKey ->
                    moveOwnedToken previousKey newKey target token
                | _ ->
                    match credit with
                    | Some _ -> ()
                    | None -> acquireOwnedToken sessionId (Some newPhysicalUserMessageId) target |> ignore

                    oldKey |> Option.iter retireExecution

                Some target)

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
            | true, tokenId ->
                match tokens.TryGetValue tokenId with
                | true, token -> moveOwnedToken oldKey newKey target token
                | false, _ -> ownedTokenByExecution.Remove oldKey |> ignore
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
                match token.InFlight with
                | Some step ->
                    step.SessionId = sessionId
                    && step.PhysicalUserMessageId = physicalUserMessageId
                    && not (Set.contains providerRun step.Fence)
                | None -> false)
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
