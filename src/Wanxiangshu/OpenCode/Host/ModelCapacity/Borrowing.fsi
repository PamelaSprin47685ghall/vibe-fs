namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks

type internal BorrowingCapacity<'target> =
    new:
        ledger: CapacityLedger<'target> * providerOf: ('target -> string) * sameTarget: ('target -> 'target -> bool) ->
            BorrowingCapacity<'target>

    member BindChild: parentSessionId: string * childSessionId: string -> unit
    member BindCompanion: ownerSessionId: string * bloggerSessionId: string -> unit
    member DropLineage: sessionId: string -> unit

    member RouteFresh:
        sessionId: string *
        oldPhysicalUserMessageId: string option *
        newPhysicalUserMessageId: string *
        route: ('target array -> 'target option) ->
            'target option

    member ReserveFresh: sessionId: string * route: ('target array -> 'target option) -> 'target option
    member AdoptReservation: sessionId: string * physicalUserMessageId: string * target: 'target -> unit
    member ReleaseSession: sessionId: string -> CapacityTransitionOutcome
    member ReleasePhysical: sessionId: string * physicalUserMessageId: string -> CapacityTransitionOutcome

    member EnterStep:
        sessionId: string *
        physicalUserMessageId: string *
        target: 'target *
        fence: Set<string> *
        tryOrdinary: ('target array -> bool) ->
            Task

    member EndStep: sessionId: string * physicalUserMessageId: string * providerRun: string -> unit
    member SuppressStep: sessionId: string * physicalUserMessageId: string -> unit
    member internal ExactCredit: sessionId: string * physicalUserMessageId: string -> CapacityCreditId
    member InvariantSnapshot: unit -> BorrowingCapacitySnapshot<'target>
    member Snapshot: unit -> 'target array
    member Fail: error: exn -> unit
