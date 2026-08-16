namespace Wanxiangshu.Composition.Turn

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// JS-native boundary for reconciliation's classifiers, evidence, wakes and
/// publish seals. Maps remain opaque; turns, decisions and observations cross
/// as plain objects or stable strings.
module ReconcileSurface =

    type private PublishMapsHandle(maps: ReconcileProgram.PublishMaps) =
        member _.Maps = maps

    [<Emit("$0==null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0[$1]"

    let private stringOf (value: obj) =
        if isNullish value then "" else string value

    let private int64Of (value: obj) =
        if isNullish value then 0L else int64 (string value)

    let private boolOf (value: obj) =
        if isNullish value then false else unbox<bool> value

    let private mapsOf (value: obj) = (value :?> PublishMapsHandle).Maps

    let private outcomeOf (value: obj) : ReconcileProgram.TurnOutcome =
        ReconcileProgram.outcomeOf (stringOf value)

    let private wakeOf (value: obj) : ReconcileProgram.ReconcileWake =
        match stringOf (property value "kind") with
        | "IdleWake" ->
            ReconcileProgram.ReconcileWake.IdleWake(
                QuiescencePermit.create
                    (SessionId.create (stringOf (property value "session")))
                    (int64Of (property value "attemptSerial"))
            )
        | "RetryWake" -> ReconcileProgram.ReconcileWake.RetryWake
        | "FailureWake" -> ReconcileProgram.ReconcileWake.FailureWake
        | "AbortWake" -> ReconcileProgram.ReconcileWake.AbortWake
        | other -> invalidArg "wake" (sprintf "unknown reconcile wake: %s" other)

    let private evidenceOf (value: obj) : ReconcileProgram.ReconcileEvidence =
        match stringOf (property value "kind") with
        | "SnapshotError" -> ReconcileProgram.evidenceSnapshotError (stringOf (property value "reason"))
        | "NoTurn" -> ReconcileProgram.evidenceNoTurn ()
        | "Provisional" -> ReconcileProgram.evidenceProvisional (outcomeOf (property value "outcome"))
        | "Unknown" -> ReconcileProgram.evidenceUnknown ()
        | "Terminal" -> ReconcileProgram.evidenceTerminal (outcomeOf (property value "outcome"))
        | "SessionCleared" -> ReconcileProgram.evidenceSessionCleared ()
        | other -> invalidArg "evidence" (sprintf "unknown reconcile evidence: %s" other)

    let private decisionToJs (decision: ReconcileProgram.ReconcileDecision) (rereadsRemaining: int) : obj =
        let name = ReconcileProgram.decisionName decision
        let rereads =
            match decision with
            | ReconcileProgram.ReconcileDecision.Reread(_, remaining) -> remaining
            | ReconcileProgram.ReconcileDecision.Publish
            | ReconcileProgram.ReconcileDecision.StopPass -> rereadsRemaining

        box
            {| name = name
               clearsContinuationCandidate = ReconcileProgram.clearsContinuationCandidate decision
               rereadsRemaining = rereads |}

    let private turnOf (value: obj) : ReconcileProgram.PublishTurn =
        ReconcileProgram.turnFixture
            (stringOf (property value "session"))
            (stringOf (property value "physical"))
            (stringOf (property value "providerRun"))
            (ReconcileProgram.outcomeOf (stringOf (property value "outcome")))

    let private turnObject (session: string) (physical: string) (providerRun: string) (outcome: string) : obj =
        box {| session = session; physical = physical; providerRun = providerRun; outcome = outcome |}

    let empty () : obj = PublishMapsHandle(ReconcileProgram.publishMapsEmpty()) :> obj

    let turnFixture (value: obj) : obj =
        turnObject
            (stringOf (property value "session"))
            (stringOf (property value "physical"))
            (stringOf (property value "providerRun"))
            (stringOf (property value "outcome"))

    /// Stable JS contract for the fields accepted by `turnFixture` and the
    /// publish seal operations. This is an owner-defined vocabulary view, not
    /// Fable reflection metadata.
    let acceptedTurnFields () : string array =
        [| "session"; "physical"; "providerRun"; "outcome" |]

    /// Classify a publishable turn from its JS outcome name. `TurnUnknown` is
    /// deliberately rejected by the domain classifier: it is a snapshot
    /// observation, never a business turn.
    let classifyTurn (outcomeName: string) : obj =
        let outcome = ReconcileProgram.outcomeOf outcomeName
        let terminal = ReconcileProgram.isTerminalOutcome outcome

        box
            {| outcome = outcomeName
               state = if terminal then "terminal" else "provisional"
               isTerminal = terminal |}

    let isTerminalOutcome (outcomeName: string) : bool =
        ReconcileProgram.outcomeOf outcomeName |> ReconcileProgram.isTerminalOutcome

    /// These predicates keep structural clean-break checks at the owner boundary
    /// without exporting the underlying union constructors or case metadata.
    let isPublishableOutcome (outcomeName: string) : bool =
        outcomeName <> "TurnUnknown" && outcomeName <> "AbortWake"

    let isSnapshotObservation (observationName: string) : bool =
        observationName = "TurnUnknown"

    let tryOutcome (outcomeName: string) : obj =
        try
            let outcome = ReconcileProgram.outcomeOf outcomeName
            let canonicalName =
                match outcomeName with
                | "TurnInProgress"
                | "TurnNeedsContinuation"
                | "TurnCompleted"
                | "TurnAborted"
                | "TurnFailed" -> outcomeName
                | _ ->
                    if ReconcileProgram.isTerminalOutcome outcome then "TurnFailed" else "TurnInProgress"

            box {| accepted = true; name = canonicalName |}
        with error ->
            box {| accepted = false; error = error.Message |}

    // ── wake and evidence observations ───────────────────────────────────────

    let idleWake (session: string) (attemptSerial: int64) : obj =
        box
            {| kind = "IdleWake"
               hasQuiescence = true
               session = session
               attemptSerial = attemptSerial |}

    let retryWake () : obj =
        box {| kind = "RetryWake"; hasQuiescence = false |}

    let failureWake () : obj =
        box {| kind = "FailureWake"; hasQuiescence = false |}

    let abortWake () : obj =
        box {| kind = "AbortWake"; hasQuiescence = false |}

    let evidenceSnapshotError (reason: string) : obj =
        box {| kind = "SnapshotError"; reason = reason |}

    let evidenceNoTurn () : obj = box {| kind = "NoTurn" |}

    let evidenceProvisional (outcomeName: string) : obj =
        box {| kind = "Provisional"; outcome = outcomeName |}

    let evidenceUnknown () : obj = box {| kind = "Unknown" |}

    let evidenceTerminal (outcomeName: string) : obj =
        box {| kind = "Terminal"; outcome = outcomeName |}

    let evidenceSessionCleared () : obj = box {| kind = "SessionCleared" |}

    let decideStep (wake: obj) (rereadsRemaining: int) (evidence: obj) : obj =
        ReconcileProgram.decideStep (wakeOf wake) rereadsRemaining (evidenceOf evidence)
        |> fun decision -> decisionToJs decision rereadsRemaining

    let decisionName (decision: obj) : string = stringOf (property decision "name")

    let clearsContinuationCandidate (decision: obj) : bool =
        boolOf (property decision "clearsContinuationCandidate")

    let consumeKey (turn: obj) : string =
        ReconcileProgram.consumeKey (turnOf turn)

    let provisionalHas (maps: obj) (turn: obj) : bool =
        (mapsOf maps).provisionalHas(turnOf turn)

    let consumedHas (maps: obj) (turn: obj) : bool =
        (mapsOf maps).consumedHas(turnOf turn)

    let publishDecision (maps: obj) (turn: obj) : obj =
        let result = ReconcileProgram.publishDecision (mapsOf maps) (turnOf turn)
        box {| shouldPublish = result.shouldPublish; maps = PublishMapsHandle(result.maps) :> obj |}

    let clearProvisional (maps: obj) (session: string) : obj =
        PublishMapsHandle(ReconcileProgram.clearProvisional (mapsOf maps) session) :> obj
