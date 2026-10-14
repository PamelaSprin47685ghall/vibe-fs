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
    let private mkdirSync (path: string) (options: obj): unit = jsNative

    let private ensureDir (path: string) =
        mkdirSync path (createObj [ "recursive" ==> true ])

    [<Emit("$0 != null && typeof $0 === 'object'")>]
    let private isRecord (_value: obj): bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isString (_value: obj): bool = jsNative

    [<Emit("Array.isArray($0) ? $0 : []")>]
    let private asArray (_value: obj): obj array = jsNative

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
    let resultText (receipt: HostReceipt) (name: string) : string =
        text (nested receipt.Result name)

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

    let private closeConnection (connection: obj) : unit =
        connection?close ()

    /// One open, one close. SqliteJournal.close closes the connection it was given.
    /// If the journal is never constructed, this function closes the connection itself.
    let useWork (gitCommonDir: string) (workId: string) (body: obj -> 'a) : 'a =
        let path = journalPath gitCommonDir
        ensureDir (parentOf path)
        let connection = createNew DatabaseSync path
        let mutable journal: obj option = None

        try
            let opened: obj = createNew SqliteJournal connection
            journal <- Some opened
            let work: obj = createNew DurableWork (workId, opened)
            body work
        finally
            match journal with
            | Some opened -> opened?close ()
            | None -> closeConnection connection

    let execute (gitCommonDir: string) (workId: string) (requestId: string) (command: obj) : HostReceipt =
        useWork gitCommonDir workId (fun work ->
            let durable = work?execute (requestId, command)
            observe requestId durable)

    let inspect (gitCommonDir: string) (workId: string) : obj =
        useWork gitCommonDir workId (fun work -> work?inspect ())

    let private textOf (value: obj) : string =
        if isNull value then "" else string value

    /// Fields the old readers already used. Nothing else is projected.
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
          Root: string option }

    let inspected (gitCommonDir: string) (workId: string) : InspectedWork option =
        // One open, one close for the snapshot and the history it needs. The
        // blocker digest is the last recorded RetirementBlocked for the active
        // incumbency; every other field is already on the snapshot.
        useWork gitCommonDir workId (fun work ->
            let snapshot: obj = work?inspect ()
            let opened = unbox<bool> snapshot?opened
            if not opened then
                None
            else
                let active: obj = snapshot?active
                let activeId =
                    if isNull active then None else Some(textOf active?id)
                let outcome =
                    if isNull active then
                        None
                    else
                        let seen = textOf active?reviewOutcome
                        if seen = "" then None else Some seen
                let openingStage =
                    if isNull active then
                        None
                    else
                        let seen = textOf active?openingStage
                        if seen = "" then None else Some seen
                let openingCursor =
                    if isNull active then None else Some(unbox<float> active?openingCursor)

                let rounds = unbox<obj array> snapshot?rounds

                // Every IncumbencyRetired this work has committed, in order.
                let retirements =
                    unbox<obj array> (work?history ())
                    |> Array.collect (fun frame ->
                        let events: obj array = unbox frame?events
                        events)
                    |> Array.filter (fun event -> textOf event?``type`` = "IncumbencyRetired")

                // The predecessor range is the retirement range the current
                // tenure inherited: the last round before it opened. The
                // library records both cursors per round, so this needs no
                // second bookkeeping.
                let predecessorRange =
                    match activeId with
                    | None -> None
                    | Some _ when rounds.Length = 0 -> None
                    | Some _ ->
                        let last = rounds.[rounds.Length - 1]
                        let retirement = last?retirementCursor
                        if isNull retirement then
                            None
                        else
                            Some(unbox<float> last?openingCursor, unbox<float> retirement)

                let sequence =
                    if isNull snapshot?sequence then
                        []
                    else
                        unbox<obj array> snapshot?sequence |> Array.toList |> List.map textOf

                let blockerDigest =
                    match activeId with
                    | None -> None
                    | Some id ->
                        retirements
                        |> Array.choose (fun event ->
                            if
                                textOf event?``type`` = "RetirementBlocked"
                                && textOf event?incumbencyId = id
                            then
                                let digest = textOf event?blockerDigest
                                if digest = "" then None else Some digest
                            else
                                None)
                        |> Array.tryLast

                let retiredRuns =
                    retirements
                    |> Array.choose (fun event ->
                        let run = textOf event?providerRunId
                        if run <> "" then
                            Some run
                        else
                            let cut = nested event "projectionCut"
                            let cutRun = if isRecord cut then textOf cut?providerRunId else ""
                            if cutRun = "" then None else Some cutRun)
                    |> Array.toList

                let devopsId = textOf snapshot?devopsId
                let root = textOf snapshot?root
                let confirmed =
                    not (isNull active) && unbox<bool> active?confirmed
                Some
                    { Opened = true
                      Sequence = sequence
                      Stage = textOf snapshot?stage
                      ActiveId = activeId
                      OpeningStage = openingStage
                      Reviewed = outcome.IsSome
                      ReviewOutcome = outcome
                      OpeningCursor = openingCursor
                      PredecessorRange = predecessorRange
                      DeliveryAuthorized = unbox<bool> snapshot?deliveryAuthorized
                      Delivered = isNull active && not (isNull snapshot?opened)
                      PendingDelivery = not (isNull snapshot?pendingDelivery)
                      RoundCount = rounds.Length
                      RetiredRuns = retiredRuns
                      DevOpsId = if devopsId = "" then None else Some devopsId
                      Authority = textOf snapshot?authority
                      Confirmed = confirmed
                      BlockerDigest = blockerDigest
                      Root = if root = "" then None else Some root })

    let recover (gitCommonDir: string) (workId: string) : obj =
        useWork gitCommonDir workId (fun work -> work?recover ())
