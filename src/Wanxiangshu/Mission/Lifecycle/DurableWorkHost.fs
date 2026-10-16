namespace Wanxiangshu.Mission.Lifecycle

open System
open Fable.Core
open Fable.Core.JsInterop

/// Host side of wanxiangli durable work. Opens and closes the sqlite journal
/// in this module. Callers send a public command and receive a host observation.
/// They do not hold the connection.
module DurableWorkHost =

    [<Import("createRequire", "node:module")>]
    let private createRequire (_url: string) : obj = jsNative

    [<Emit("import.meta.url")>]
    let private moduleUrl: string = jsNative

    [<Emit("typeof globalThis.Bun !== 'undefined'")>]
    let private runningOnBun () : bool = jsNative

    [<Emit("$0($1)")>]
    let private requireModule (_require: obj) (_name: string) : obj = jsNative

    [<Import("SqliteJournal", "wanxiangli/eventstore")>]
    let private SqliteJournal: obj = jsNative

    [<Import("DurableWork", "wanxiangli/durable-work")>]
    let private DurableWork: obj = jsNative

    [<Import("Work", "wanxiangli/work")>]
    let private Work: obj = jsNative

    [<Import("Ownership", "wanxiangli/ownership")>]
    let private Ownership: obj = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string) (options: obj) : unit = jsNative

    let private ensureDir (path: string) =
        mkdirSync path (createObj [ "recursive" ==> true ])

    let private openDatabase (path: string) : obj =
        let require = createRequire moduleUrl

        if runningOnBun () then
            let sqlite = requireModule require "bun:sqlite"
            createNew sqlite?Database path
        else
            let sqlite = requireModule require "node:sqlite"
            createNew sqlite?DatabaseSync path

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

    let private closeUnreturnedConnection (connection: obj) =
        try
            connection?close ()
        with _ ->
            ()

    let private openJournal (path: string) : obj =
        let connection = openDatabase path

        try
            createNew SqliteJournal connection
        with error ->
            closeUnreturnedConnection connection
            raise error

    let useWork (gitCommonDir: string) (workId: string) (body: obj -> 'a) : 'a =
        let path = journalPath gitCommonDir
        ensureDir (parentOf path)
        let opened = openJournal path

        try
            let work: obj = createNew DurableWork (workId, opened)
            body work
        finally
            opened?close ()

    let private appendSeed (journal: obj) (workId: string) (revision: float) (record: string) : float =
        let frame: obj = emitJsExpr record "JSON.parse($0)"
        let requestId = text (nested frame "requestId")
        let acknowledgement: obj = journal?append (workId, revision, requestId, record)
        let status = text (nested acknowledgement "status")

        if status <> "Committed" && status <> "Replayed" then
            failwith (status + ": " + text (nested acknowledgement "reason"))

        unbox<float> (nested acknowledgement "revision")

    /// Test/probe host for canonical records. It uses the same runtime-specific
    /// SQLite selection as the durable path and does not expose the connection.
    let useEphemeralWork (workId: string) (records: string array) (body: obj -> 'a) : 'a =
        let opened = openJournal ":memory:"

        try
            records |> Array.fold (appendSeed opened workId) 0. |> ignore
            body (createNew DurableWork (workId, opened))
        finally
            opened?close ()

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
          PendingOperationId: string option
          PendingExecSessionId: string option
          PendingDigest: string option
          PendingPath: string option
          RoundCount: int
          RetiredRuns: string list
          DevOpsId: string option
          DevOpsTarget: string option
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

    let private pendingText (pending: obj) (name: string) : string option =
        if isNull pending then
            None
        else
            optionalText (textOf pending?(name))

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
        let pending: obj = snapshot?pendingDelivery
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
          PendingDelivery = not (isNull pending)
          PendingOperationId = pendingText pending "operationId"
          PendingExecSessionId = pendingText pending "execSessionId"
          PendingDigest = pendingText pending "digest"
          PendingPath = pendingText pending "path"
          RoundCount = rounds.Length
          RetiredRuns = retirements |> Array.choose retiredRunOf |> Array.toList
          DevOpsId = optionalText (textOf snapshot?devopsId)
          DevOpsTarget =
            eventsOfType "DevOpsBound" history
            |> Array.tryLast
            |> Option.bind (fun event -> optionalText (textOf event?target))
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
        // Read once. Snapshot and history must describe the same revision.
        useWork gitCommonDir workId (fun durable ->
            let records: string array = durable?records ()
            let work: obj = createNew Work (workId, records)
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

    let private openingFailure (receipt: HostReceipt) =
        if receipt.Reason = "" then
            receipt.Status
        else
            receipt.Reason

    let private expectedSequence (value: obj) =
        asArray value |> Array.toList |> List.map text

    let private matchingOpening (root: string) (sequence: obj) (work: InspectedWork) : Result<InspectedWork, string> =
        let expected = expectedSequence sequence

        if work.Sequence <> expected then
            Error "existing lifecycle work has a different sequence"
        elif work.Root <> Some root then
            Error "existing lifecycle work has a different root"
        else
            Ok work

    /// Idempotent host opening across process races. An in-memory precheck is
    /// not a lock: Conflict or Unknown can still mean another writer committed
    /// the same work. Only a durable reread decides whether opening is settled.
    let ensureOpened
        (gitCommonDir: string)
        (workId: string)
        (requestId: string)
        (root: string)
        (sequence: obj)
        (ownerId: string)
        (openingCursor: float)
        : Result<InspectedWork, string> =
        let readExisting () =
            inspected gitCommonDir workId |> Option.map (matchingOpening root sequence)

        let openMissing () =
            let issued =
                command
                    "Open"
                    [ "root", box root
                      "sequence", sequence
                      "ownerId", box ownerId
                      "openingCursor", box openingCursor ]

            let receipt = execute gitCommonDir workId requestId issued

            match receipt.Status with
            | "Committed"
            | "Replayed"
            | "Conflict"
            | "Unknown" -> readExisting () |> Option.defaultValue (Error(openingFailure receipt))
            | _ -> Error(openingFailure receipt)

        match readExisting () with
        | Some result -> result
        | None -> openMissing ()

    let recover (gitCommonDir: string) (workId: string) : obj =
        useWork gitCommonDir workId (fun work -> work?recover ())

    type OwnershipLease =
        { RegistryId: string
          ResourceId: string
          Token: string }

    type OwnershipBlocker =
        { Lease: OwnershipLease
          ScopeId: string
          Kind: string
          Phase: string
          ExternalId: string option
          IncarnationId: string option
          Outcome: string option
          Reason: string option
          CancellationRequested: bool }

    let ownershipMessage (kind: string) (fields: (string * obj) list) : obj =
        createObj (("type", box kind) :: fields)

    let leaseObject (lease: OwnershipLease) : obj =
        createObj
            [ "registryId", box lease.RegistryId
              "resourceId", box lease.ResourceId
              "token", box lease.Token ]

    let bindingObject (externalId: string) (incarnationId: string) : obj =
        createObj [ "externalId", box externalId; "incarnationId", box incarnationId ]

    let private buildLease (registryId: string) (resourceId: string) (token: string) : OwnershipLease option =
        if registryId = "" || resourceId = "" || token = "" then
            None
        else
            Some
                { RegistryId = registryId
                  ResourceId = resourceId
                  Token = token }

    let parseLease (value: obj) : OwnershipLease option =
        if not (isRecord value) then
            None
        else
            buildLease
                (text (nested value "registryId"))
                (text (nested value "resourceId"))
                (text (nested value "token"))

    let private bindingField (binding: obj) (name: string) : string option =
        if isRecord binding then
            optionalText (text (nested binding name))
        else
            None

    let private parseBlockerItem (item: obj) : OwnershipBlocker option =
        parseLease (nested item "lease")
        |> Option.map (fun lease ->
            let binding = nested item "binding"

            { Lease = lease
              ScopeId = text (nested item "scopeId")
              Kind = text (nested item "kind")
              Phase = text (nested item "phase")
              ExternalId = bindingField binding "externalId"
              IncarnationId = bindingField binding "incarnationId"
              Outcome = optionalText (text (nested item "outcome"))
              Reason = optionalText (text (nested item "reason"))
              CancellationRequested =
                not (isNull (nested item "cancellationRequested"))
                && unbox<bool> (nested item "cancellationRequested") })

    let parseBlockers (raw: obj) : OwnershipBlocker list =
        asArray raw |> Array.choose parseBlockerItem |> Array.toList

    let createOwnership (registryId: string) (records: string array) : obj =
        createNew Ownership (registryId, records)

    let executeOwnershipInMemory (ownership: obj) (requestId: string) (message: obj) : HostReceipt =
        let reply: obj = ownership?execute (requestId, message)
        let status = text (nested reply "status")
        let result = nested reply "result"
        let kind = if isRecord result then text (result?kind) else ""
        let revisionValue = nested reply "revision"

        let revision =
            if isNull revisionValue then
                0.
            else
                unbox<float> revisionValue

        { RequestId = requestId
          Status = status
          Kind = kind
          Reason = ""
          Revision = revision
          Result = result }

    let ownershipBlockersInMemory (ownership: obj) (scopeId: string) : OwnershipBlocker list =
        parseBlockers (ownership?blockers scopeId)

    let ownershipScopePhaseInMemory (ownership: obj) (scopeId: string) : string option =
        let view: obj = ownership?scope scopeId
        optionalText (text (nested view "phase"))

    let private ownershipStream (registryId: string) : string = "ownership:" + registryId

    let private useOwnershipJournal (gitCommonDir: string) (body: obj -> 'a) : 'a =
        let path = journalPath gitCommonDir
        ensureDir (parentOf path)
        let opened = openJournal path

        try
            body opened
        finally
            opened?close ()

    let private revisionOrDefault (fallback: float) (value: obj) : float =
        if isNull value then fallback else unbox<float> value

    let private resultKind (result: obj) : string =
        if isRecord result then text (result?kind) else ""

    let private defaultUnknownReason (ackReason: string) : string =
        if ackReason = "" then
            "Unknown journal acknowledgement"
        else
            ackReason

    let private interpretOwnershipAck
        (journal: obj)
        (stream: string)
        (registryId: string)
        (requestId: string)
        (message: obj)
        (expected: float)
        (revision: float)
        (result: obj)
        (kind: string)
        (ack: obj)
        : HostReceipt =
        let ackStatus = text (nested ack "status")
        let ackReason = text (nested ack "reason")
        let committedRev = revisionOrDefault expected (nested ack "revision")

        if ackStatus = "Committed" && committedRev = revision then
            { RequestId = requestId
              Status = "Committed"
              Kind = kind
              Reason = ""
              Revision = revision
              Result = result }
        elif ackStatus = "Replayed" then
            let reread: string array = unbox (journal?read stream)
            let replay = createOwnership registryId reread
            let replayed: obj = replay?execute (requestId, message)
            let repResult = nested replayed "result"

            { RequestId = requestId
              Status = "Replayed"
              Kind = resultKind repResult
              Reason = ""
              Revision = committedRev
              Result = repResult }
        elif ackStatus = "Conflict" || ackStatus = "NotWritten" then
            { RequestId = requestId
              Status = ackStatus
              Kind = ""
              Reason = ackReason
              Revision = expected
              Result = null }
        else
            { RequestId = requestId
              Status = "Unknown"
              Kind = ""
              Reason = defaultUnknownReason ackReason
              Revision = expected
              Result = null }

    let private commitOwnershipRecord
        (journal: obj)
        (stream: string)
        (registryId: string)
        (requestId: string)
        (message: obj)
        (revision: float)
        (result: obj)
        (kind: string)
        (record: string)
        : HostReceipt =
        let expected = revision - 1.

        try
            let ack: obj = journal?append (stream, expected, requestId, record)
            interpretOwnershipAck journal stream registryId requestId message expected revision result kind ack
        with error ->
            { RequestId = requestId
              Status = "Unknown"
              Kind = ""
              Reason = error.Message
              Revision = expected
              Result = null }

    let executeOwnershipDurable
        (gitCommonDir: string)
        (registryId: string)
        (requestId: string)
        (message: obj)
        : HostReceipt =
        let stream = ownershipStream registryId

        useOwnershipJournal gitCommonDir (fun journal ->
            let records: string array = unbox (journal?read stream)
            let candidate = createOwnership registryId records
            let proposal: obj = candidate?execute (requestId, message)
            let status = text (nested proposal "status")
            let result = nested proposal "result"
            let kind = if isRecord result then text (result?kind) else ""
            let revValue = nested proposal "revision"
            let revision = if isNull revValue then 0. else unbox<float> revValue

            if status <> "Applied" then
                { RequestId = requestId
                  Status = status
                  Kind = kind
                  Reason = ""
                  Revision = revision
                  Result = result }
            else
                let record = text (nested proposal "record")
                commitOwnershipRecord journal stream registryId requestId message revision result kind record)

    let mayStartResource (receipt: HostReceipt) : OwnershipLease option =
        if receipt.Status = "Committed" && receipt.Kind = "Reserved" then
            parseLease (nested receipt.Result "lease")
        else
            None

    let mayStartInMemoryResource (receipt: HostReceipt) : OwnershipLease option =
        if receipt.Status = "Applied" && receipt.Kind = "Reserved" then
            parseLease (nested receipt.Result "lease")
        else
            None

    let ownershipBlockersDurable (gitCommonDir: string) (registryId: string) (scopeId: string) : OwnershipBlocker list =
        if String.IsNullOrWhiteSpace gitCommonDir then
            invalidArg (nameof gitCommonDir) "Ownership common directory is required"

        if String.IsNullOrWhiteSpace registryId then
            invalidArg (nameof registryId) "Ownership registry is required"

        let stream = ownershipStream registryId

        useOwnershipJournal gitCommonDir (fun journal ->
            let records: string array = unbox (journal?read stream)
            let candidate = createOwnership registryId records
            ownershipBlockersInMemory candidate scopeId)

    let tryOwnershipBlockersDurable
        (gitCommonDir: string)
        (registryId: string)
        (scopeId: string)
        : OwnershipBlocker list option =
        if String.IsNullOrWhiteSpace gitCommonDir then
            invalidArg (nameof gitCommonDir) "Ownership common directory is required"

        if String.IsNullOrWhiteSpace registryId then
            invalidArg (nameof registryId) "Ownership registry is required"

        useOwnershipJournal gitCommonDir (fun journal ->
            let records: string array = unbox (journal?read (ownershipStream registryId))

            if Array.isEmpty records then
                None
            else
                let candidate = createOwnership registryId records
                Some(ownershipBlockersInMemory candidate scopeId))
