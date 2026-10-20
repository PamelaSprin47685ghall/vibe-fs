namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// The slice of the `wanxiangli/borrowing` capacity ledger, execution custody
/// and provider-step borrowing owner (CUT-10) this host adapter reads. Token
/// acquisition/retirement, lender borrowing and its single-provider attribution,
/// custody moves, provider-step queueing, reuse, reclaim and grant order are
/// decided there; this adapter keeps the F# types (credit ids, targets, options,
/// fence sets) and maps the invariant snapshot onto the host records.
type internal CapacityBorrowingLibrary =
    abstract routeFresh:
        sessionId: string * oldPhysicalUserMessageId: obj * newPhysicalUserMessageId: string * lenderSessionId: obj *
        route: obj * retireOnUnavailable: bool ->
            obj

    abstract reserveFresh: sessionId: string * lenderSessionId: obj * route: obj -> obj
    abstract adoptReservation: sessionId: string * physicalUserMessageId: string * target: obj -> unit
    abstract continueExecution: sessionId: string * previousPhysicalId: string * physicalId: string * target: obj -> obj
    abstract releaseSession: sessionId: string -> string
    abstract releasePhysical: sessionId: string * physicalUserMessageId: string -> string

    abstract enterStep:
        sessionId: string * physicalUserMessageId: string * target: obj * fence: string array * tryOrdinary: obj *
        requestKey: obj ->
            Task

    abstract endStep: sessionId: string * physicalUserMessageId: string * providerRun: string -> unit
    abstract suppressStep: sessionId: string * physicalUserMessageId: string -> unit
    abstract exactCredit: sessionId: string * physicalUserMessageId: string -> float
    abstract invariantSnapshot: unit -> obj
    abstract snapshot: unit -> obj array
    abstract fail: error: exn -> unit

module internal CapacityBorrowingBinding =
    [<Import("CapacityBorrowing", "wanxiangli/borrowing")>]
    let private borrowingClass: obj = jsNative

    [<Emit("new $0($1)")>]
    let private construct (cls: obj) (options: obj) : CapacityBorrowingLibrary = jsNative

    let create (providerOf: 'target -> string) (sameTarget: 'target -> 'target -> bool) =
        construct
            borrowingClass
            (createObj
                [ "providerOf" ==> Func<obj, string>(fun target -> providerOf (unbox<'target> target))
                  "sameTarget"
                  ==> Func<obj, obj, bool>(fun left right -> sameTarget (unbox<'target> left) (unbox<'target> right)) ])

    let text (value: string option) : obj =
        match value with
        | Some value -> box value
        | None -> null

    let target (value: obj) : 'target option =
        if isNull value then None else Some(unbox<'target> value)

    let route (policy: 'target array -> 'target option) : obj =
        box (
            Func<obj array, obj>(fun running ->
                match policy (running |> Array.map unbox<'target>) with
                | Some selected -> box selected
                | None -> null)
        )

    let outcome (value: string) =
        match value with
        | "Applied" -> CapacityTransitionOutcome.Applied
        | "AlreadyApplied" -> CapacityTransitionOutcome.AlreadyApplied
        | other -> invalidOp (sprintf "execution-model-routing: unknown borrowing outcome '%s'" other)

    let owner (value: obj) : CapacityExactOwnerSnapshot =
        { SessionId = unbox value?sessionId
          PhysicalUserMessageId = unbox value?physicalUserMessageId
          Role = None
          Participant = None }

    let credit (value: obj) : int64 = int64 (unbox<float> value)

/// Lineage-local borrowing and preemptive recall over one capacity ledger.
/// Borrowing changes who may use a token, never how many real tokens exist.
type internal BorrowingCapacity<'target>(providerOf: 'target -> string, sameTarget: 'target -> 'target -> bool) =
    let capacity = CapacityBorrowingBinding.create providerOf sameTarget

    member _.RouteFresh
        (
            sessionId: string,
            oldPhysicalUserMessageId: string option,
            newPhysicalUserMessageId: string,
            lenderSessionId: string option,
            route: 'target array -> 'target option,
            ?retireOnUnavailable: bool
        ) : 'target option =
        capacity.routeFresh (
            sessionId,
            CapacityBorrowingBinding.text oldPhysicalUserMessageId,
            newPhysicalUserMessageId,
            CapacityBorrowingBinding.text lenderSessionId,
            CapacityBorrowingBinding.route route,
            defaultArg retireOnUnavailable true
        )
        |> CapacityBorrowingBinding.target

    member _.ReserveFresh(sessionId: string, lenderSessionId: string option, route: 'target array -> 'target option) : 'target option =
        capacity.reserveFresh (sessionId, CapacityBorrowingBinding.text lenderSessionId, CapacityBorrowingBinding.route route)
        |> CapacityBorrowingBinding.target

    member _.AdoptReservation(sessionId: string, physicalUserMessageId: string, target: 'target) =
        capacity.adoptReservation (sessionId, physicalUserMessageId, box target)

    member _.ContinueExecution(sessionId: string, previousPhysicalId: string, physicalId: string, target: 'target) : 'target =
        capacity.continueExecution (sessionId, previousPhysicalId, physicalId, box target) |> unbox<'target>

    member _.ReleaseSession(sessionId: string) =
        capacity.releaseSession sessionId |> CapacityBorrowingBinding.outcome

    member _.ReleasePhysical(sessionId: string, physicalUserMessageId: string) =
        capacity.releasePhysical (sessionId, physicalUserMessageId) |> CapacityBorrowingBinding.outcome

    member _.EnterStep
        (
            sessionId: string,
            physicalUserMessageId: string,
            target: 'target,
            fence: Set<string>,
            tryOrdinary: 'target array -> bool,
            ?requestKey: string
        ) : Task =
        capacity.enterStep (
            sessionId,
            physicalUserMessageId,
            box target,
            Set.toArray fence,
            box (Func<obj array, bool>(fun running -> tryOrdinary (running |> Array.map unbox<'target>))),
            CapacityBorrowingBinding.text requestKey
        )

    member _.EndStep(sessionId: string, physicalUserMessageId: string, providerRun: string) =
        capacity.endStep (sessionId, physicalUserMessageId, providerRun)

    member _.SuppressStep(sessionId: string, physicalUserMessageId: string) =
        capacity.suppressStep (sessionId, physicalUserMessageId)

    member internal _.ExactCredit(sessionId: string, physicalUserMessageId: string) =
        capacity.exactCredit (sessionId, physicalUserMessageId)
        |> int64
        |> CapacityCreditId.ofValue

    member _.InvariantSnapshot() : BorrowingCapacitySnapshot<'target> =
        let snapshot = capacity.invariantSnapshot ()

        { LedgerEntries =
            unbox<obj array> snapshot?ledgerEntries
            |> Array.map (fun entry ->
                { Credit = CapacityBorrowingBinding.credit entry?credit
                  Target = unbox<'target> entry?target })
          Tokens =
            unbox<obj array> snapshot?tokens
            |> Array.map (fun token ->
                { Credit = CapacityBorrowingBinding.credit token?credit
                  State = unbox<string> token?state
                  Owner = CapacityBorrowingBinding.owner token?owner
                  Target = unbox<'target> token?target })
          Custodies =
            unbox<obj array> snapshot?custodies
            |> Array.map (fun custody ->
                { Credit = CapacityBorrowingBinding.credit custody?credit
                  Owner = CapacityBorrowingBinding.owner custody?owner })
          Waiters =
            unbox<obj array> snapshot?waiters
            |> Array.map (fun waiter ->
                { Owner = CapacityBorrowingBinding.owner waiter?owner
                  Sequence = CapacityBorrowingBinding.credit waiter?sequence
                  Kind = unbox<string> waiter?kind })
          Lineage = Array.empty
          IdleCount = unbox<int> snapshot?idleCount
          InFlightCount = unbox<int> snapshot?inFlightCount
          RetiringCount = unbox<int> snapshot?retiringCount }

    member _.Snapshot() : 'target array =
        capacity.snapshot () |> Array.map unbox<'target>

    member _.Fail(error: exn) = capacity.fail error
