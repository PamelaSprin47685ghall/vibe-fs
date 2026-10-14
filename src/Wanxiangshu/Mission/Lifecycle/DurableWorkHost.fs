namespace Wanxiangshu.Mission.Lifecycle

open System
open Fable.Core
open Fable.Core.JsInterop

/// Host side of wanxiangli durable work. Opens and closes the sqlite journal
/// in this module. Callers send a public command and receive a host observation.
/// They do not hold the connection.
module DurableWorkHost =

    [<Import("DatabaseSync", "node:sqlite")>]
    let private DatabaseSync: obj = jsNative

    [<Import("SqliteJournal", "wanxiangli/eventstore")>]
    let private SqliteJournal: obj = jsNative

    [<Import("DurableWork", "wanxiangli/durable-work")>]
    let private DurableWork: obj = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string) (options: obj) : unit = jsNative

    let private ensureDir (path: string) =
        mkdirSync path (createObj [ "recursive" ==> true ])

    [<Emit("$0 != null && typeof $0 === 'object'")>]
    let private isRecord (_value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (_value: obj) : bool = jsNative

    [<Emit("Array.isArray($0) ? $0 : []")>]
    let private asArray (_value: obj) : obj array = jsNative

    /// Sibling of `.git/wanxiangshu/events/`, not inside it. The library takes a
    /// connection, not a path; this file is the host-owned journal for new work.
    let journalPath (gitCommonDir: string) : string =
        gitCommonDir.TrimEnd('/') + "/wanxiangshu/work.sqlite"

    type HostReceipt =
        { RequestId: string
          Status: string
          Kind: string
          Reason: string
          Revision: float
          Result: obj }

    let private text (value: obj) : string =
        if isString value then unbox value else ""

    let private nested (value: obj) (name: string) : obj =
        if isRecord value then value?(name) else null

    /// The request id that entered is the request id that leaves. This function
    /// does not mint a replacement.
    let observe (requestId: string) (durableResult: obj) : HostReceipt =
        let status = text (nested durableResult "status")
        let result = nested durableResult "result"
        let kind = if isRecord result then text (result?kind) else ""
        let reasonValue = nested durableResult "reason"
        let revisionValue = nested durableResult "revision"

        let revision =
            if isNull revisionValue then
                0.
            else
                unbox<float> revisionValue

        { RequestId = requestId
          Status = status
          Kind = kind
          Reason = text reasonValue
          Revision = revision
          Result = result }

    let private admittedStatus status =
        status = "Committed" || status = "Replayed" || status = "Observed"

    let admitted (receipt: HostReceipt) : bool =
        admittedStatus receipt.Status && not (String.IsNullOrEmpty receipt.Kind)

    /// Published decision fields. DeliveryRequested carries operationId, digest, and path.
    let resultText (receipt: HostReceipt) (name: string) : string = text (nested receipt.Result name)

    /// First physical start is allowed only for a committed DeliveryRequested.
    /// Replayed, Observed, Unknown, NotWritten, Conflict, and any other tag are not a start.
    let mayStart (durableResult: obj) : bool =
        let seen = observe "" durableResult
        seen.Status = "Committed" && seen.Kind = "DeliveryRequested"

    let command (kind: string) (fields: (string * obj) list) : obj =
        createObj (("type", box kind) :: fields)

    let planSequence () : obj = box [| "S1"; "S2"; "S3" |]

    let executionSequence () : obj = box [| "S4" |]

    let private parentOf (path: string) : string =
        let slash = path.LastIndexOf('/')
        if slash <= 0 then path else path.Substring(0, slash)

    let private closeConnection (connection: obj) : unit = connection?close ()

    let private closeJournal (journal: obj option) (connection: obj) : unit =
        match journal with
        | Some opened -> opened?close ()
        | None -> closeConnection connection

    /// One open, one close. SqliteJournal.close closes the connection it was given.
    /// If the journal is never constructed, this function closes the connection itself.
    let useWork (gitCommonDir: string) (workId: string) (body: obj -> 'a) : 'a =
        let path = journalPath gitCommonDir
        ensureDir (parentOf path)
        let connection = createNew DatabaseSync path
        // DSL-MUTABLE: resource — the journal this call opened, closed exactly once in finally
        let mutable journal: obj option = None

        try
            let opened: obj = createNew SqliteJournal connection
            journal <- Some opened
            let work: obj = createNew DurableWork (workId, opened)
            body work
        finally
            closeJournal journal connection

    let execute (gitCommonDir: string) (workId: string) (requestId: string) (command: obj) : HostReceipt =
        useWork gitCommonDir workId (fun work ->
            let durable = work?execute (requestId, command)
            observe requestId durable)

    let inspect (gitCommonDir: string) (workId: string) : obj =
        useWork gitCommonDir workId (fun work -> work?inspect ())

    let private textOf (value: obj) : string =
        if isNull value then "" else string value

    /// A provider run / tool call pair as recorded by a committed lifecycle fact.
    type HostRunFence =
        { ProviderRunId: string
          ToolCallId: string }

    /// One committed IncumbencyRetired fact. ProjectionCut is true when the
    /// retirement came from the execution sequence and carries no cursor.
    type HostRetirement =
        { RetirementId: string
          IncumbencyId: string
          Outcome: string
          Fence: HostRunFence
          ProjectionCut: bool }

    /// One committed AdvanceCommitted fact: which tenure moved authority from
    /// Expected to Next.
    type HostAdvance =
        { IncumbencyId: string
          Expected: string
          Next: string }

    /// Fields the old readers already used, plus the retirement list,
    /// confirmation identity, projection cut, and advance history the
    /// recovery points need.
    type InspectedWork =
        { Opened: bool
          Sequence: string list
          Stage: string
          ActiveId: string option
          OpeningStage: string option
          Reviewed: bool
          ReviewOutcome: string option
          OpeningCursor: float option
          PredecessorRange: (float * float) option
          DeliveryAuthorized: bool
          Delivered: bool
          PendingDelivery: bool
          RoundCount: int
          RetiredRuns: string list
          DevOpsId: string option
          Authority: string
          Confirmed: bool
          BlockerDigest: string option
          Root: string option
          Retirements: HostRetirement list
          Confirmation: HostRunFence option
          ProjectionCut: HostRunFence option
          Advances: HostAdvance list }

    let private optionalText (value: string) : string option = if value = "" then None else Some value

    let private activeIdOf (active: obj) : string option =
        if isNull active then None else Some(textOf active?id)

    let private reviewOutcomeOf (active: obj) : string option =
        if isNull active then
            None
        else
            let seen = textOf active?reviewOutcome
            if seen = "" then None else Some seen

    let private openingStageOf (active: obj) : string option =
        if isNull active then
            None
        else
            let seen = textOf active?openingStage
            if seen = "" then None else Some seen

    let private openingCursorOf (active: obj) : float option =
        if isNull active then
            None
        else
            Some(unbox<float> active?openingCursor)

    let private confirmedOf (active: obj) : bool =
        not (isNull active) && unbox<bool> active?confirmed

    let private sequenceOf (snapshot: obj) : string list =
        if isNull snapshot?sequence then
            []
        else
            unbox<obj array> snapshot?sequence |> Array.toList |> List.map textOf

    let private historyEventsOf (work: obj) : obj array =
        unbox<obj array> (work?history ())
        |> Array.collect (fun frame -> unbox<obj array> frame?events)

    let private eventsOfType (name: string) (events: obj array) : obj array =
        events |> Array.filter (fun event -> textOf event?``type`` = name)

    let private hostRunFenceOf (event: obj) : HostRunFence =
        let cut = nested event "projectionCut"
        let source = if isRecord cut then cut else event

        { ProviderRunId = textOf source?providerRunId
          ToolCallId = textOf source?toolCallId }

    let private hostRetirementOf (event: obj) : HostRetirement =
        let cut = nested event "projectionCut"

        { RetirementId = textOf event?retirementId
          IncumbencyId = textOf event?incumbencyId
          Outcome = textOf event?outcome
          Fence = hostRunFenceOf event
          ProjectionCut = isRecord cut }

    let private hostAdvanceOf (event: obj) : HostAdvance =
        { IncumbencyId = textOf event?incumbencyId
          Expected = textOf event?expected
          Next = textOf event?next }

    let private cutRunOf (event: obj) : string option =
        let cut = nested event "projectionCut"
        let run = if isRecord cut then textOf cut?providerRunId else ""
        if run = "" then None else Some run

    let private retiredRunOf (event: obj) : string option =
        let run = textOf event?providerRunId
        if run <> "" then Some run else cutRunOf event

    let private rangeOfLastRound (last: obj) : (float * float) option =
        let retirement = last?retirementCursor

        if isNull retirement then
            None
        else
            Some(unbox<float> last?openingCursor, unbox<float> retirement)

    let private predecessorRangeOf (activeId: string option) (rounds: obj array) : (float * float) option =
        match activeId, rounds with
        | None, _
        | Some _, [||] -> None
        | Some _, _ -> rangeOfLastRound rounds.[rounds.Length - 1]

    let private blockerDigestOf (activeId: string option) (events: obj array) : string option =
        match activeId with
        | None -> None
        | Some id ->
            events
            |> Array.choose (fun event ->
                let blocked =
                    textOf event?``type`` = "RetirementBlocked" && textOf event?incumbencyId = id

                if blocked then Some(textOf event?blockerDigest) else None)
            |> Array.choose (fun digest -> if digest = "" then None else Some digest)
            |> Array.tryLast

    let private projectionCutOf (retirements: obj array) : HostRunFence option =
        retirements
        |> Array.filter (fun event -> isRecord (nested event "projectionCut"))
        |> Array.tryLast
        |> Option.map hostRunFenceOf

    let private projectedWork (snapshot: obj) (work: obj) : InspectedWork =
        let active: obj = snapshot?active
        let activeId = activeIdOf active
        let outcome = reviewOutcomeOf active
        let rounds = unbox<obj array> snapshot?rounds
        let history = historyEventsOf work
        let retirements = eventsOfType "IncumbencyRetired" history

        let confirmation =
            eventsOfType "ConfirmationCommitted" history
            |> Array.tryLast
            |> Option.map hostRunFenceOf

        { Opened = true
          Sequence = sequenceOf snapshot
          Stage = textOf snapshot?stage
          ActiveId = activeId
          OpeningStage = openingStageOf active
          Reviewed = outcome.IsSome
          ReviewOutcome = outcome
          OpeningCursor = openingCursorOf active
          PredecessorRange = predecessorRangeOf activeId rounds
          DeliveryAuthorized = unbox<bool> snapshot?deliveryAuthorized
          Delivered = isNull active && not (isNull snapshot?opened)
          PendingDelivery = not (isNull snapshot?pendingDelivery)
          RoundCount = rounds.Length
          RetiredRuns = retirements |> Array.choose retiredRunOf |> Array.toList
          DevOpsId = optionalText (textOf snapshot?devopsId)
          Authority = textOf snapshot?authority
          Confirmed = confirmedOf active
          BlockerDigest = blockerDigestOf activeId history
          Root = optionalText (textOf snapshot?root)
          Retirements = retirements |> Array.map hostRetirementOf |> Array.toList
          Confirmation = confirmation
          ProjectionCut = projectionCutOf retirements
          Advances =
            eventsOfType "AdvanceCommitted" history
            |> Array.map hostAdvanceOf
            |> Array.toList }

    let private inspectedJournal (gitCommonDir: string) (workId: string) : InspectedWork option =
        // One open, one close for the snapshot and the history it needs.
        useWork gitCommonDir workId (fun work ->
            let snapshot: obj = work?inspect ()

            if unbox<bool> snapshot?opened then
                Some(projectedWork snapshot work)
            else
                None)

    let inspected (gitCommonDir: string) (workId: string) : InspectedWork option =
        // An empty id names no work; it never reaches the journal as a query.
        if String.IsNullOrEmpty workId then
            None
        else
            inspectedJournal gitCommonDir workId

    let recover (gitCommonDir: string) (workId: string) : obj =
        useWork gitCommonDir workId (fun work -> work?recover ())
