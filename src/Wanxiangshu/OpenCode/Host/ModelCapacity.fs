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
    // DSL-MUTABLE: resource — monotonic opaque capacity-credit identity
    let mutable nextCredit = 0L

    member _.Acquire(target: 'target) =
        lock gate (fun () ->
            nextCredit <- nextCredit + 1L
            entries.[nextCredit] <- target
            nextCredit)

    member _.Retarget(credit: int64, target: 'target) =
        lock gate (fun () ->
            if entries.ContainsKey credit then
                entries.[credit] <- target)

    member _.Release(credit: int64) =
        lock gate (fun () -> entries.Remove credit)

    member _.Entries() =
        lock gate (fun () ->
            entries
            |> Seq.map (fun (KeyValue(credit, target)) -> credit, target)
            |> Seq.toArray)

    member this.Snapshot() = this.Entries() |> Array.map snd

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
    { Credit: int64
      mutable OwnerKey: string
      OwnerSessionId: string
      Provider: string
      mutable OwnerTarget: 'target
      mutable State: CapacityCreditState }

type private CapacityStepDemand<'target> =
    { Sequence: int64
      SessionId: string
      PhysicalUserMessageId: string
      Target: 'target
      Fence: Set<string>
      TryOrdinary: 'target array -> bool
      Completion: TaskCompletionSource<unit> }

type private CapacityCreditSource =
    { Credit: int64
      LenderSessionId: string
      Distance: int }

/// Decorates the old ledger with lineage-local borrowing and preemptive recall.
/// Borrowing changes who may use a token, never how many real tokens exist.
type BorrowingCapacity<'target>
    (ledger: CapacityLedger<'target>, providerOf: 'target -> string, sameTarget: 'target -> 'target -> bool) =
    let gate = obj ()
    /// DSL-cross-callback-proof: physical resource — capacity lineage used only to route token borrowing/recall
    // DSL-MUTABLE: resource — capacity-only session lineage
    let parents = Dictionary<string, string>()
    // DSL-MUTABLE: resource — capacity-only Main → Blogger companion association
    let companionSessionByOwner = Dictionary<string, string>()
    // DSL-MUTABLE: resource — capacity-only Blogger → Main companion association
    let companionOwnerBySession = Dictionary<string, string>()
    /// DSL-cross-callback-proof: physical resource — exact execution-to-lender token ownership
    // DSL-MUTABLE: resource — exact borrowed execution → actual lender token
    let creditSourceByExecution = Dictionary<string, CapacityCreditSource>()
    // DSL-MUTABLE: resource — at most one owned capacity token per execution
    let ownedTokenByExecution = Dictionary<string, int64>()
    // DSL-MUTABLE: resource — decorator metadata for ledger tokens
    let tokens = Dictionary<int64, CapacityCredit<'target>>()
    // DSL-MUTABLE: resource — provider-step admission waiters
    let waiters = ResizeArray<CapacityStepDemand<'target>>()
    let mutable nextDemand = 0L

    let executionKey sessionId physicalUserMessageId =
        sessionId + "\u001f" + (physicalUserMessageId |> Option.defaultValue "")

    let executionPrefix sessionId = sessionId + "\u001f"

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

    let clearCreditSourcesForToken (tokenId: int64) =
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

    let isAncestor ancestor descendant =
        ancestorDistance ancestor descendant |> Option.isSome

    let tryCompanionOwner sessionId =
        match companionOwnerBySession.TryGetValue sessionId with
        | true, owner -> Some owner
        | false, _ -> None

    let tryCompanionSession ownerSessionId =
        match companionSessionByOwner.TryGetValue ownerSessionId with
        | true, companion -> Some companion
        | false, _ -> None

    let companionLender (requester: string) : (string * int) option =
        currentCreditSource requester
        |> Option.map (fun source -> source.LenderSessionId, source.Distance)
        |> Option.orElseWith (fun () ->
            tryCompanionOwner requester
            |> Option.bind currentCreditSource
            |> Option.bind (fun source ->
                tryCompanionSession source.LenderSessionId
                |> Option.map (fun lenderCompanion -> lenderCompanion, source.Distance)))

    let matchingLenderDistance lenderSessionId (candidateLender, distance) =
        if candidateLender = lenderSessionId then
            Some distance
        else
            None

    let creditDistance lenderSessionId requester =
        if lenderSessionId = requester then
            Some 0
        elif companionOwnerBySession.ContainsKey requester then
            companionLender requester
            |> Option.bind (matchingLenderDistance lenderSessionId)
        else
            ancestorDistance lenderSessionId requester

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

    let creditTokens requester =
        tokens.Values
        |> Seq.choose (fun token ->
            if isRetiring token then
                None
            else
                creditDistance token.OwnerSessionId requester
                |> Option.map (fun distance -> token, distance))
        |> Seq.groupBy (fun (token, _) -> token.Provider)
        |> Seq.map (fun (provider, candidates) ->
            let token, _ =
                candidates
                |> Seq.sortBy (fun (token, distance) -> distance, token.Credit)
                |> Seq.head

            provider, token)
        |> Map.ofSeq

    let withoutTokens tokenIds =
        ledger.Entries()
        |> Array.choose (fun (token, target) -> if Set.contains token tokenIds then None else Some target)

    let schedulingView requester =
        let credits = creditTokens requester

        let hidden =
            credits |> Seq.map (fun (KeyValue(_, token)) -> token.Credit) |> Set.ofSeq

        withoutTokens hidden, credits

    let ordinaryDecision (route: 'target array -> 'target option) : ('target * CapacityCredit<'target> option) option =
        route (ledger.Snapshot()) |> Option.map (fun target -> target, None)

    let attributedDecision target (credit: CapacityCredit<'target>) route =
        match route (withoutTokens (Set.singleton credit.Credit)) with
        | Some attributable when sameTarget attributable target -> Some(target, Some credit)
        | _ -> ordinaryDecision route

    let matchingCreditDecision target (credits: Map<string, CapacityCredit<'target>>) route =
        match Map.tryFind (normalizeProvider target) credits with
        | None -> ordinaryDecision route
        | Some credit when credits.Count = 1 -> Some(target, Some credit)
        | Some credit -> attributedDecision target credit route

    /// A combined borrowed view is only a tentative convenience. When multiple
    /// provider credits were hidden, the chosen target must still be reproducible
    /// with exactly its own provider token hidden; otherwise no single token can
    /// legally pay for that decision.
    let routeDecision requester route : ('target * CapacityCredit<'target> option) option =
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
        | CapacityCreditState.Idle -> ledger.Retarget(token.Credit, target)
        | CapacityCreditState.InFlight _
        | CapacityCreditState.Retiring _ -> ()

    let finishStep (token: CapacityCredit<'target>) =
        match token.State with
        | CapacityCreditState.Idle -> ()
        | CapacityCreditState.InFlight _ ->
            token.State <- CapacityCreditState.Idle
            ledger.Retarget(token.Credit, token.OwnerTarget)
        | CapacityCreditState.Retiring _ -> releaseToken token

    let reconcileFence sessionId physicalUserMessageId fence =
        tokens.Values
        |> Seq.tryFind (fun token ->
            match token.State with
            | CapacityCreditState.InFlight step
            | CapacityCreditState.Retiring step ->
                step.SessionId = sessionId && step.PhysicalUserMessageId = physicalUserMessageId
            | CapacityCreditState.Idle -> false)
        |> Option.iter (fun token ->
            match token.State with
            | CapacityCreditState.InFlight step
            | CapacityCreditState.Retiring step when not (Set.isEmpty (Set.difference fence step.Fence)) ->
                finishStep token
            | _ -> ())

    let rememberGrantedCredit (token: CapacityCredit<'target>) (demand: CapacityStepDemand<'target>) =
        match creditDistance token.OwnerSessionId demand.SessionId with
        | Some distance ->
            rememberCreditSource
                (executionKey demand.SessionId (Some demand.PhysicalUserMessageId))
                demand.SessionId
                token
                distance
        | None -> invalidOp "execution-model-routing: granted capacity token is not a legal credit source"

    let grant (token: CapacityCredit<'target>) (demand: CapacityStepDemand<'target>) =
        match token.State with
        | CapacityCreditState.Idle ->
            ledger.Retarget(token.Credit, demand.Target)
            rememberGrantedCredit token demand

            token.State <-
                CapacityCreditState.InFlight
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

    let borrowPair (demand: CapacityStepDemand<'target>) (token: CapacityCredit<'target>) =
        let provider = normalizeProvider demand.Target

        match token.State, creditDistance token.OwnerSessionId demand.SessionId with
        | CapacityCreditState.Idle, Some distance when token.Provider = provider ->
            Some(distance, demand.Sequence, token.Credit, demand, token)
        | _ -> None

    let idleBorrowPairs () =
        waiters
        |> Seq.collect (fun demand -> tokens.Values |> Seq.choose (borrowPair demand))
        |> Seq.toList

    let tryGrantBorrowed () =
        match
            idleBorrowPairs ()
            |> List.sortBy (fun (distance, sequence, token, _, _) -> distance, sequence, token)
        with
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

    let acquireForRoute sessionId physicalUserMessageId target (credit: CapacityCredit<'target> option) =
        match credit with
        | Some _ -> ()
        | None -> acquireOwnedToken sessionId physicalUserMessageId target |> ignore

    let requiredCreditDistance failure lenderSessionId requester =
        match creditDistance lenderSessionId requester with
        | Some distance -> distance
        | None -> invalidOp failure

    let recordRoutedCredit sessionId key (credit: CapacityCredit<'target> option) =
        match credit with
        | Some token ->
            requiredCreditDistance
                "execution-model-routing: routed credit is not legal for requester"
                token.OwnerSessionId
                sessionId
            |> rememberCreditSource key sessionId token
        | None -> clearCreditSource key

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

    let commitRoutedTarget sessionId oldKey newKey newPhysicalUserMessageId target credit =
        applyRoutedToken sessionId oldKey newKey newPhysicalUserMessageId target credit
        oldKey |> Option.iter clearCreditSource
        recordRoutedCredit sessionId newKey credit
        target

    let ensureReservationToken sessionId target (credit: CapacityCredit<'target> option) =
        match credit with
        | Some _ -> ()
        | None -> acquireOwnedToken sessionId None target |> ignore

    let recordReservationCredit sessionId key (credit: CapacityCredit<'target> option) =
        match credit with
        | Some token ->
            requiredCreditDistance
                "execution-model-routing: reserved credit is not legal for requester"
                token.OwnerSessionId
                sessionId
            |> rememberCreditSource key sessionId token
        | None -> clearCreditSource key

    let adoptOwnedToken oldKey newKey target tokenId =
        match tokens.TryGetValue tokenId with
        | true, token -> moveOwnedToken oldKey newKey target token
        | false, _ -> ownedTokenByExecution.Remove oldKey |> ignore

    let dropOwnedCompanion sessionId =
        match companionSessionByOwner.TryGetValue sessionId with
        | true, blogger ->
            companionSessionByOwner.Remove sessionId |> ignore
            companionOwnerBySession.Remove blogger |> ignore
        | false, _ -> ()

    let clearOwnedCompanionIfMatches owner sessionId =
        match companionSessionByOwner.TryGetValue owner with
        | true, blogger when blogger = sessionId -> companionSessionByOwner.Remove owner |> ignore
        | _ -> ()

    let dropCompanionOwner sessionId =
        match companionOwnerBySession.TryGetValue sessionId with
        | true, owner ->
            companionOwnerBySession.Remove sessionId |> ignore
            clearOwnedCompanionIfMatches owner sessionId
        | false, _ -> ()

    member _.BindChild(parentSessionId: string, childSessionId: string) =
        lock gate (fun () ->
            if
                String.IsNullOrWhiteSpace parentSessionId
                || String.IsNullOrWhiteSpace childSessionId
            then
                invalidArg "sessionId" "execution-model-routing: capacity lineage requires non-empty session ids"

            let parent = parentSessionId.Trim()
            let child = childSessionId.Trim()

            if parent = child || isAncestor child parent then
                invalidOp "execution-model-routing: capacity lineage cycle"

            match parents.TryGetValue child with
            | true, existing when existing <> parent ->
                invalidOp (sprintf "execution-model-routing: capacity child %s changed parent" child)
            | _ -> parents.[child] <- parent)

    member _.BindCompanion(ownerSessionId: string, bloggerSessionId: string) =
        lock gate (fun () ->
            if
                String.IsNullOrWhiteSpace ownerSessionId
                || String.IsNullOrWhiteSpace bloggerSessionId
            then
                invalidArg "sessionId" "execution-model-routing: capacity companion requires non-empty session ids"

            let owner = ownerSessionId.Trim()
            let blogger = bloggerSessionId.Trim()

            if owner = blogger then
                invalidOp "execution-model-routing: capacity companion cannot own itself"

            match companionOwnerBySession.TryGetValue blogger with
            | true, existing when existing <> owner ->
                invalidOp (sprintf "execution-model-routing: blogger %s changed companion owner" blogger)
            | _ -> ()

            match companionSessionByOwner.TryGetValue owner with
            | true, existing when existing <> blogger -> companionOwnerBySession.Remove existing |> ignore
            | _ -> ()

            companionSessionByOwner.[owner] <- blogger
            companionOwnerBySession.[blogger] <- owner)

    member _.DropLineage(sessionId: string) =
        lock gate (fun () ->
            parents.Remove sessionId |> ignore

            parents.Keys
            |> Seq.filter (fun child -> parents.[child] = sessionId)
            |> Seq.toArray
            |> Array.iter (fun child -> parents.Remove child |> ignore)

            dropOwnedCompanion sessionId
            dropCompanionOwner sessionId

            creditSourceByExecution
            |> Seq.choose (fun (KeyValue(key, source)) ->
                if source.LenderSessionId = sessionId then
                    Some key
                else
                    None)
            |> Seq.toArray
            |> Array.iter clearCreditSource)

    member _.RouteFresh
        (
            sessionId: string,
            oldPhysicalUserMessageId: string option,
            newPhysicalUserMessageId: string,
            route: 'target array -> 'target option
        ) =
        lock gate (fun () ->
            let oldKey =
                oldPhysicalUserMessageId
                |> Option.map (fun physical -> executionKey sessionId (Some physical))

            let newKey = executionKey sessionId (Some newPhysicalUserMessageId)

            match routeDecision sessionId route with
            | None ->
                oldKey |> Option.iter retireExecution
                None
            | Some(target, credit) ->
                commitRoutedTarget sessionId oldKey newKey newPhysicalUserMessageId target credit
                |> Some)

    member _.ReserveFresh(sessionId: string, route: 'target array -> 'target option) =
        lock gate (fun () ->
            match routeDecision sessionId route with
            | None -> None
            | Some(target, credit) ->
                ensureReservationToken sessionId target credit

                let key = executionKey sessionId None
                recordReservationCredit sessionId key credit
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
            cancelWaiters (fun demand -> demand.SessionId = sessionId)
            clearCreditSourcesForSession sessionId

            ownedTokenByExecution.Keys
            |> Seq.filter (fun key -> key.StartsWith(sessionId + "\u001f", StringComparison.Ordinal))
            |> Seq.toArray
            |> Array.iter retireExecution

            drain ())

    member _.ReleasePhysical(sessionId: string, physicalUserMessageId: string) =
        lock gate (fun () ->
            cancelWaiters (fun demand ->
                demand.SessionId = sessionId
                && demand.PhysicalUserMessageId = physicalUserMessageId)

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

            let completion =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

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
