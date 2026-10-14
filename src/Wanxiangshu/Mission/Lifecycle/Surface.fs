namespace Wanxiangshu.Mission.Lifecycle

open System
open System.Threading.Tasks
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Persistence.EventStore

module LifecycleSurface =

    let private sha256 = HostDigest.sha256Hex

    let authorityInitial (workId: string) = LifecycleIdentity.authorityInitial sha256 workId

    let observeDurable (requestId: string) (durableResult: obj) =
        let seen = DurableWorkHost.observe requestId durableResult
        box
            {| requestId = seen.RequestId
               status = seen.Status
               kind = seen.Kind
               reason = seen.Reason |}

    let mayStartDelivery (durableResult: obj) = DurableWorkHost.mayStart durableResult

    let journalPath (gitCommonDir: string) = DurableWorkHost.journalPath gitCommonDir


    [<Import("DatabaseSync", "node:sqlite")>]
    let private DatabaseSync: obj = jsNative

    [<Import("SqliteJournal", "wanxiangli/eventstore")>]
    let private SqliteJournal: obj = jsNative

    [<Import("DurableWork", "wanxiangli/durable-work")>]
    let private DurableWork: obj = jsNative

    [<Emit("JSON.parse($0)")>]
    let private parseJson (_text: string) : obj = jsNative

    [<Emit("Number($0)")>]
    let private asNumber (_value: int64) : float = jsNative

    [<Emit("$0 != null && typeof $0 === 'object'")>]
    let private isRecord (_value: obj) : bool = jsNative

    let private remembered = Dictionary<string, string array>()

    let private recall (workId: string) =
        let found, records = remembered.TryGetValue workId
        if found then records else [||]

    let private remember (workId: string) (records: string array) =
        remembered.[workId] <- Array.copy records

    let private command (kind: string) (fields: (string * obj) list) =
        createObj (("type", box kind) :: fields)

    let private sequenceOf (kind: string) =
        if kind = "S4" || kind = "spawn" then box [| "S4" |] else box [| "S1"; "S2"; "S3" |]

    let private recordsOf (text: string) =
        text.Split('\n') |> Array.filter (fun line -> line.Length > 0)

    let private workIdOf (records: string array) =
        if records.Length = 0 then
            "unread"
        else
            let events: obj array = unbox (parseJson records.[0])?events
            match events |> Array.tryFind (fun event -> string (event?``type``) = "WorkOpened") with
            | Some event -> string (event?workId)
            | None -> "unread"

    let private namesOfRecord (record: string) =
        if record.Length = 0 then
            ""
        else
            let events: obj array = unbox (parseJson record)?events
            events |> Array.map (fun event -> string (event?``type``)) |> String.concat ","

    let private joinRecords (records: string array) =
        if records.Length = 0 then "" else (records |> String.concat "\n") + "\n"

    let private useJournal (workId: string) (records: string array) (body: obj -> 'a) : 'a =
        let connection: obj = createNew DatabaseSync ":memory:"
        let mutable journal: obj option = None
        try
            let opened: obj = createNew SqliteJournal connection
            journal <- Some opened
            let mutable revision = 0.
            for record in records do
                let requestId = string (parseJson record)?requestId
                let ack: obj = opened?append (workId, revision, requestId, record)
                let status = string ack?status
                if status <> "Committed" then
                    failwith (status + ": " + string ack?reason)
                revision <- unbox<float> ack?revision
            body (createNew DurableWork (workId, opened))
        finally
            match journal with
            | Some opened -> opened?close ()
            | None -> connection?close ()

    let private openCommand (root: string) (kind: string) (head: float) =
        command "Open" [
            "root", box root
            "sequence", sequenceOf kind
            "ownerId", box "owner"
            "openingCursor", box head
        ]

    let private reviewCommand (incumbencyId: string) (outcome: string) =
        let findings: obj =
            match outcome with
            | "Promoted" -> box ([||]: string array)
            | "Stayed" -> box [| "stayed" |]
            | _ -> null
        command "Review" [
            "incumbencyId", box incumbencyId
            "reviewerId", box "reviewer"
            "findings", findings
        ]

    let private deliverCommand (incumbencyId: string) (providerRunId: string) (toolCallId: string) (head: float) (draft: (string * string) option) =
        let fields =
            [ "incumbencyId", box incumbencyId
              "providerRunId", box providerRunId
              "toolCallId", box toolCallId
              "cursor", box head
              "blockers", box ([||]: string array)
              "nextOwnerId", box "owner" ]

        match draft with
        | None -> command "Deliver" fields
        | Some(digest, path) -> command "Deliver" (fields @ [ "draft", box {| digest = digest; path = path |} ])

    let private lastOf (records: string array) =
        if records.Length = 0 then "" else records.[records.Length - 1]

    let private shape (failed: bool) (error: string) (status: string) (kind: string) (reason: string) (wrote: bool) (record: string) (authorized: bool) (authority: string) =
        if failed then
            box
                {| decision = "Refuse"
                   detail = error
                   names = ""
                   bytes = ""
                   authorized = authorized
                   authority = authority |}
        else
            match status, wrote with
            | "Applied", true
            | "Committed", true ->
                box
                    {| decision = "Write"
                       detail = kind
                       names = namesOfRecord record
                       bytes = record + "\n"
                       authorized = authorized
                       authority = authority |}
            | "Replayed", _ ->
                box
                    {| decision = "Done"
                       detail = status
                       names = ""
                       bytes = ""
                       authorized = authorized
                       authority = authority |}
            | "Observed", _ ->
                box
                    {| decision = "Done"
                       detail = kind
                       names = ""
                       bytes = ""
                       authorized = authorized
                       authority = authority |}
            | _ ->
                box
                    {| decision = "Refuse"
                       detail = if reason.Length = 0 then status else status + ": " + reason
                       names = ""
                       bytes = ""
                       authorized = authorized
                       authority = authority |}

    let private attempt (workId: string) (records: string array) (requestId: string) (command: obj) =
        try
            useJournal workId records (fun work ->
                let prior: obj = work?inspect ()
                let priorAuthority = if isNull prior?authority then "" else string prior?authority
                let authorizedPrior = if isNull prior?deliveryAuthorized then false else unbox<bool> prior?deliveryAuthorized
                let before: string array = unbox (work?records ())
                try
                    let result: obj = work?execute (requestId, command)
                    let after: string array = unbox (work?records ())
                    let snapshot: obj = work?inspect ()
                    let decision = result?result
                    let kind = if isRecord decision then string decision?kind else ""
                    let reason = if isNull result?reason then "" else string result?reason
                    shape false "" (string result?status) kind reason (after.Length > before.Length) (lastOf after) (unbox<bool> snapshot?deliveryAuthorized) (if isNull snapshot?authority then priorAuthority else string snapshot?authority)
                with error ->
                    shape true error.Message "" "" "" false "" authorizedPrior priorAuthority)
        with error ->
            shape true error.Message "" "" "" false "" false ""

    let encodeOpening (kind: string) (workId: string) (root: string) (head: int64) =
        let seen = attempt workId [||] ("open:" + workId) (openCommand root kind (asNumber head))
        let decision = string seen?decision
        if decision = "Write" then string seen?bytes else "REFUSE " + string seen?detail

    let readProbe (text: string) =
        let records = recordsOf text
        let workId = workIdOf records
        try
            useJournal workId records (fun work ->
                let snapshot: obj = work?inspect ()
                let recovery: obj = work?recover ()
                let history: obj array = unbox (work?history ())
                let names =
                    history
                    |> Array.collect (fun frame ->
                        let events: obj array = unbox frame?events
                        events |> Array.map (fun event -> string (event?``type``)))
                    |> String.concat ","
                let requestIds =
                    history |> Array.map (fun frame -> string frame?requestId) |> String.concat ","
                let sequence =
                    if isNull snapshot?sequence then
                        ""
                    else
                        let items: obj array = unbox snapshot?sequence
                        items |> Array.map string |> String.concat ","
                let active: obj = snapshot?active
                box
                    {| ok = true
                       next = string recovery?next
                       opened = unbox<bool> snapshot?opened
                       stage = if isNull snapshot?stage then "" else string snapshot?stage
                       sequence = sequence
                       names = names
                       revision = unbox<float> snapshot?revision
                       authorized = unbox<bool> snapshot?deliveryAuthorized
                       reason = ""
                       activeId = if isNull active then "" else string active?id
                       requestIds = requestIds |})
        with error ->
            box
                {| ok = false
                   next = ""
                   opened = false
                   stage = ""
                   sequence = ""
                   names = ""
                   revision = 0.
                   authorized = false
                   reason = error.Message
                   activeId = ""
                   requestIds = "" |}

    let reviewProbe (text: string) (incumbencyId: string) (outcome: string) =
        match outcome with
        | "Promoted"
        | "Stayed" ->
            let records = recordsOf text
            attempt (workIdOf records) records ("review:" + incumbencyId + ":" + outcome) (reviewCommand incumbencyId outcome)
        | _ ->
            box
                {| decision = "Refuse"
                   detail = "review outcome rejected"
                   names = ""
                   bytes = ""
                   authorized = false
                   authority = "" |}

    let finalPromotionProbe (text: string) =
        let records = recordsOf text
        let workId = workIdOf records
        try
            useJournal workId records (fun work ->
                let mutable steps = 0
                let mutable authorized = false
                let mutable stage = ""
                while steps < 4 && not authorized do
                    steps <- steps + 1
                    let snapshot: obj = work?inspect ()
                    let active: obj = snapshot?active
                    if isNull active then
                        authorized <- unbox<bool> snapshot?deliveryAuthorized
                        stage <- if isNull snapshot?stage then "" else string snapshot?stage
                        steps <- 4
                    else
                        let id = string active?id
                        let reviewed: obj = work?execute ("promote:" + string steps + ":" + id, reviewCommand id "Promoted")
                        ignore reviewed
                        let after: obj = work?inspect ()
                        authorized <- unbox<bool> after?deliveryAuthorized
                        stage <- if isNull after?stage then "" else string after?stage
                        if not authorized && not (isNull after?active) then
                            let cursor = unbox<float> after?active?openingCursor + float steps
                            let delivered: obj =
                                work?execute (
                                    "promote-deliver:" + string steps,
                                    deliverCommand (string after?active?id) "run-final" ("call-" + string steps) cursor None
                                )
                            ignore delivered
                            let followed: obj = work?inspect ()
                            authorized <- unbox<bool> followed?deliveryAuthorized
                            stage <- if isNull followed?stage then "" else string followed?stage
                box {| authorized = authorized; detail = stage |})
        with error ->
            box {| authorized = false; detail = error.Message |}

    let private supplied (draft: obj) =
        emitJsExpr draft "$0 != null && $0 !== undefined"

    let private draftPair (draft: obj) =
        if not (supplied draft) then
            None
        else
            Some(string draft?digest, string draft?path)

    let deliverProbe (text: string) (providerRunId: string) (toolCallId: string) (head: int64) (draft: obj) =
        let records = recordsOf text
        let workId = workIdOf records
        let incumbencyId =
            if records.Length = 0 then
                ""
            else
                try
                    useJournal workId records (fun work ->
                        let snapshot: obj = work?inspect ()
                        if isNull snapshot?active then "" else string snapshot?active?id)
                with _ -> ""
        let selected = draftPair draft
        let requested =
            attempt workId records ("deliver:" + providerRunId + ":" + toolCallId) (deliverCommand incumbencyId providerRunId toolCallId (asNumber head) selected)
        if selected.IsNone || string requested?decision <> "Write" || string requested?detail <> "DeliveryRequested" then
            requested
        else
            let digest, path = selected.Value
            let operationId =
                let events: obj array = unbox (parseJson (string requested?bytes))?events
                string (events |> Array.find (fun event -> string event?``type`` = "DeliveryRequested"))?operationId
            let observed =
                attempt
                    workId
                    (Array.append records (recordsOf (string requested?bytes)))
                    operationId
                    (command
                        "ResolveDelivery"
                        [ "operationId", box operationId
                          "outcome", box "Accepted"
                          "execSessionId", box ("exec:" + operationId)
                          "digest", box digest
                          "path", box path
                          "blockers", box ([||]: string array) ])
            if string observed?decision <> "Write" then
                observed
            else
                box
                    {| decision = "Write"
                       detail = "delivered"
                       names = namesOfRecord (string observed?bytes)
                       bytes = string requested?bytes + string observed?bytes
                       authorized = unbox<bool> observed?authorized
                       authority = string observed?authority |}

    let replayProbe (text: string) (providerRunId: string) (toolCallId: string) (head: int64) =
        deliverProbe text providerRunId toolCallId head null

    let deliverStateProbe (text: string) (providerRunId: string) (toolCallId: string) (head: int64) (draft: obj) =
        deliverProbe text providerRunId toolCallId head draft

    let replayStateProbe (text: string) (providerRunId: string) (toolCallId: string) (head: int64) (draft: obj) =
        let first = deliverProbe text providerRunId toolCallId head draft
        let written = string first?bytes
        let nextText = if written.Length = 0 then text else text + written
        let second = deliverProbe nextText providerRunId toolCallId head draft
        box
            {| decision = string second?decision
               detail = string second?detail
               names = string second?names
               bytes = string second?bytes
               original = string first?detail |}

    let private retirementIdOf (text: string) (incumbencyId: string) =
        recordsOf text
        |> Array.tryPick (fun record ->
            let events: obj array = unbox (parseJson record)?events
            events
            |> Array.tryPick (fun event ->
                if string (event?``type``) = "IncumbencyRetired" && string event?incumbencyId = incumbencyId then
                    Some(string event?retirementId)
                else
                    None))

    let reopenProbe (text: string) (incumbencyId: string) =
        let records = recordsOf text
        let workId = workIdOf records
        let retirementId = retirementIdOf text incumbencyId |> Option.defaultValue incumbencyId
        let cursor =
            try
                useJournal workId records (fun work ->
                    let snapshot: obj = work?inspect ()
                    let rounds: obj array = if isNull snapshot?rounds then [||] else unbox snapshot?rounds
                    if rounds.Length = 0 then 0. else unbox<float> rounds.[rounds.Length - 1]?retirementCursor)
            with _ -> 0.
        let reopen =
            command "Reopen" [
                "retirementId", box retirementId
                "ownerId", box "owner"
                "openingCursor", box cursor
                "blockers", box ([||]: string array)
            ]
        attempt workId records ("reopen:" + retirementId) reopen

    let commitProbe (name: string) =
        match name with
        | "Committed"
        | "Replayed"
        | "Conflict"
        | "NotWritten"
        | "Unknown" -> name
        | _ -> "not-durable"

    let private produceOpenReview (workId: string) =
        useJournal workId [||] (fun work ->
            let opened: obj = work?execute ("open:" + workId, openCommand "/repo" "plan" 7.)
            ignore opened
            let snapshot: obj = work?inspect ()
            let id = string snapshot?active?id
            let reviewed: obj = work?execute ("review:" + id + ":Stayed", reviewCommand id "Stayed")
            ignore reviewed
            let records: string array = unbox (work?records ())
            Array.copy records)

    let advance (workId: string) (prior: string) (expected: string) (next: string) =
        try
            let records = Array.append (produceOpenReview workId) (recordsOf prior)
            useJournal workId records (fun work ->
                let priorSnap: obj = work?inspect ()
                let authority = if isNull priorSnap?authority then "" else string priorSnap?authority
                let active: obj = priorSnap?active
                if isNull active then
                    box
                        {| decision = "Refuse"
                           detail = "no active incumbency"
                           names = ""
                           bytes = ""
                           authorized = false
                           authority = authority |}
                else
                    let advanceCommand =
                        command "Advance" [
                            "incumbencyId", box (string active?id)
                            "expected", box expected
                            "next", box next
                        ]
                    try
                        let result: obj = work?execute ("advance:" + expected + ":" + next, advanceCommand)
                        let after: string array = unbox (work?records ())
                        let snapshot: obj = work?inspect ()
                        let decision = result?result
                        let kind = if isRecord decision then string decision?kind else ""
                        let status = string result?status
                        let record = lastOf after
                        if status = "Committed" then
                            box
                                {| decision = "Write"
                                   detail = kind
                                   names = namesOfRecord record
                                   bytes = record + "\n"
                                   authorized = unbox<bool> snapshot?deliveryAuthorized
                                   authority = string snapshot?authority |}
                        else
                            box
                                {| decision = "Done"
                                   detail = kind
                                   names = ""
                                   bytes = ""
                                   authorized = unbox<bool> snapshot?deliveryAuthorized
                                   authority = authority |}
                    with error ->
                        box
                            {| decision = "Refuse"
                               detail = error.Message
                               names = ""
                               bytes = ""
                               authorized = false
                               authority = authority |})
        with error ->
            box
                {| decision = "Refuse"
                   detail = error.Message
                   names = ""
                   bytes = ""
                   authorized = false
                   authority = "" |}

    let private runSeed (workId: string) (requestId: string) (issued: obj) =
        try
            Ok(
                useJournal workId (recall workId) (fun work ->
                    let before: string array = unbox (work?records ())
                    let result: obj = work?execute (requestId, issued)
                    let after: string array = unbox (work?records ())
                    remember workId after
                    result, after.Length > before.Length)
            )
        with error ->
            Error error.Message

    let private stampedDir (store: obj) : string option =
        let raw = emitJsExpr store "$0 && typeof $0.gitCommonDir === 'string' ? $0.gitCommonDir : ''"
        let text = if isNull raw then "" else string raw
        if String.IsNullOrWhiteSpace text then None else Some text

    let seedOpen (store: obj) (kind: string) (workId: string) (root: string) (head: int64) : Task<obj> =
        task {
            match stampedDir store with
            | Some dir ->
                let issued = openCommand root kind (asNumber head)
                let receipt = DurableWorkHost.execute dir workId ("open:" + workId) issued
                if receipt.Status = "Committed" || receipt.Status = "Replayed" then
                    return box {| ok = true; opened = true; error = "" |}
                else
                    return box {| ok = false; opened = false; error = if receipt.Reason = "" then receipt.Status else receipt.Reason |}
            | None ->
            if (recall workId).Length > 0 then
                return box {| ok = true; opened = false; error = "" |}
            else
                match runSeed workId ("open:" + workId) (openCommand root kind (asNumber head)) with
                | Ok(result, wrote) ->
                    let status = string result?status
                    return box {| ok = status = "Committed" || status = "Replayed"; opened = wrote; error = "" |}
                | Error reason -> return box {| ok = false; opened = false; error = reason |}
        }

    let seedReview (store: obj) (workId: string) (outcome: string) : Task<obj> =
        task {
            match stampedDir store, outcome with
            | Some dir, ("Promoted" | "Stayed") ->
                match DurableWorkHost.inspected dir workId with
                | None -> return box {| ok = false; reviewed = false; error = "lifecycle work is missing" |}
                | Some work ->
                    match work.ActiveId with
                    | None -> return box {| ok = false; reviewed = false; error = "no active incumbency" |}
                    | Some incumbencyId ->
                        let receipt =
                            DurableWorkHost.execute dir workId ("review:" + workId) (reviewCommand incumbencyId outcome)
                        if receipt.Status = "Committed" || receipt.Status = "Replayed" then
                            return box {| ok = true; reviewed = true; error = "" |}
                        else
                            return
                                box
                                    {| ok = false
                                       reviewed = false
                                       error = if receipt.Reason = "" then receipt.Status else receipt.Reason |}
            | _, _ ->
            match outcome with
            | "Promoted"
            | "Stayed" ->
                if (recall workId).Length = 0 then
                    return box {| ok = false; reviewed = false; error = "lifecycle work is missing" |}
                else
                    let incumbencyId =
                        try
                            useJournal workId (recall workId) (fun work ->
                                let snapshot: obj = work?inspect ()
                                if isNull snapshot?active then "" else string snapshot?active?id)
                        with error -> error.Message
                    if incumbencyId.Length = 0 then
                        return box {| ok = false; reviewed = false; error = "no active incumbency" |}
                    else
                        match runSeed workId ("review:" + incumbencyId + ":" + outcome) (reviewCommand incumbencyId outcome) with
                        | Ok(result, wrote) ->
                            let status = string result?status
                            return box {| ok = status = "Committed" || status = "Replayed"; reviewed = wrote; error = "" |}
                        | Error reason -> return box {| ok = false; reviewed = false; error = reason |}
            | _ -> return box {| ok = false; reviewed = false; error = "review outcome rejected" |}
        }

    let seedDeliver (store: obj) (workId: string) (providerRunId: string) (toolCallId: string) (head: int64) : Task<obj> =
        task {
            match stampedDir store with
            | Some dir ->
                match DurableWorkHost.inspected dir workId with
                | None -> return box {| ok = false; decision = "missing"; error = "lifecycle work is missing" |}
                | Some work ->
                    let incumbencyId = work.ActiveId |> Option.defaultValue ""
                    let receipt =
                        DurableWorkHost.execute
                            dir
                            workId
                            ("deliver:" + providerRunId + ":" + toolCallId)
                            (deliverCommand incumbencyId providerRunId toolCallId (asNumber head) None)
                    let kind = if receipt.Kind.Length = 0 then receipt.Status else receipt.Kind
                    if receipt.Status = "Committed" || receipt.Status = "Replayed" || receipt.Status = "Observed" then
                        return box {| ok = true; decision = kind; error = "" |}
                    else
                        return
                            box
                                {| ok = false
                                   decision = "Refuse"
                                   error = if receipt.Reason = "" then receipt.Status else receipt.Reason |}
            | None ->
            if (recall workId).Length = 0 then
                return box {| ok = false; decision = "missing"; error = "lifecycle work is missing" |}
            else
                let incumbencyId =
                    try
                        useJournal workId (recall workId) (fun work ->
                            let snapshot: obj = work?inspect ()
                            if isNull snapshot?active then "" else string snapshot?active?id)
                    with error -> error.Message
                match runSeed workId ("deliver:" + providerRunId + ":" + toolCallId) (deliverCommand incumbencyId providerRunId toolCallId (asNumber head) None) with
                | Ok(result, _) ->
                    let status = string result?status
                    let decision = result?result
                    let kind = if isRecord decision then string decision?kind else status
                    return box {| ok = status = "Committed" || status = "Replayed" || status = "Observed"; decision = kind; error = "" |}
                | Error reason -> return box {| ok = false; decision = "Refuse"; error = reason |}
        }

    let seedConfirmation (store: obj) (workId: string) (providerRunId: string) (toolCallId: string) : Task<obj> =
        task {
            match stampedDir store with
            | Some dir ->
                match DurableWorkHost.inspected dir workId with
                | None -> return box {| ok = false; error = "lifecycle work is missing" |}
                | Some work ->
                    let incumbencyId = work.ActiveId |> Option.defaultValue ""
                    let receipt =
                        DurableWorkHost.execute
                            dir
                            workId
                            ("deliver:" + providerRunId + ":" + toolCallId)
                            (deliverCommand incumbencyId providerRunId toolCallId (asNumber 1L) None)
                    if (receipt.Status = "Committed" || receipt.Status = "Replayed") && receipt.Kind = "Confirmed" then
                        return box {| ok = true; error = "" |}
                    else
                        let refusal =
                            if receipt.Kind.Length > 0 then receipt.Kind
                            elif receipt.Reason.Length > 0 then receipt.Reason
                            else receipt.Status
                        return box {| ok = false; error = refusal |}
            | None ->
                if (recall workId).Length = 0 then
                    return box {| ok = false; error = "lifecycle work is missing" |}
                else
                    let! delivered = seedDeliver store workId providerRunId toolCallId 1L
                    let admitted = unbox<bool> delivered?ok
                    if admitted && string delivered?decision = "Confirmed" then
                        return box {| ok = true; error = "" |}
                    else
                        return box {| ok = false; error = string delivered?decision + " " + string delivered?error |}
        }

    let documentView (text: string) =
        let records = recordsOf text
        let workId = workIdOf records
        try
            useJournal workId records (fun work ->
                let snapshot: obj = work?inspect ()
                let recovery: obj = work?recover ()
                if not (unbox<bool> snapshot?opened) then
                    box {| found = false; detail = "not opened" |}
                else
                    let active: obj = snapshot?active
                    let rounds: obj array = if isNull snapshot?rounds then [||] else unbox snapshot?rounds
                    let history: obj array = unbox (work?history ())
                    let mutable digest = ""
                    let mutable path = ""
                    let mutable execSessionId = ""
                    for frame in history do
                        let events: obj array = unbox frame?events
                        for event in events do
                            if string (event?``type``) = "IncumbencyRetired" && string event?outcome = "Delivered" then
                                if not (isNull event?digest) then digest <- string event?digest
                                if not (isNull event?path) then path <- string event?path
                                if not (isNull event?execSessionId) then execSessionId <- string event?execSessionId
                    let predecessor =
                        if rounds.Length = 0 then
                            ""
                        else
                            let round = rounds.[rounds.Length - 1]
                            string round?openingCursor + "," + string round?retirementCursor
                    let latest =
                        if rounds.Length = 0 then
                            ""
                        else
                            let round = rounds.[0]
                            string round?openingCursor + "," + string round?retirementCursor
                    box
                        {| found = true
                           detail = ""
                           workId = string snapshot?workId
                           root = string snapshot?root
                           projection = string snapshot?stage
                           authorized = unbox<bool> snapshot?deliveryAuthorized
                           active = not (isNull active)
                           activeId = if isNull active then "" else string active?id
                           openingStage = if isNull active then "" else string active?openingStage
                           openingCursor = if isNull active then 0. else unbox<float> active?openingCursor
                           retiredCount = rounds.Length
                           delivered = isNull active && string recovery?next = "Done"
                           digest = digest
                           path = path
                           execSessionId = execSessionId
                           devopsId = if isNull snapshot?devopsId then "" else string snapshot?devopsId
                           predecessor = predecessor
                           latestRetirement = latest |})
        with error ->
            box {| found = false; detail = error.Message |}

    let private readFaultText (fault: ReadFault) =
        match fault with
        | ReadFault.StorageInvalid reason
        | ReadFault.PairIncomplete reason
        | ReadFault.Rejected reason -> reason

    let private workOf (text: string) =
        LifecycleCodec.readDocument text
        |> Result.mapError readFaultText
        |> Result.bind LifecycleFold.applyAll

    let decodeDocument (text: string) =
        match LifecycleCodec.readDocument text with
        | Error fault ->
            box
                {| ok = false
                   names = ""
                   fault = readFaultText fault |}
        | Ok events ->
            box
                {| ok = true
                   names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat ","
                   fault = "" |}

    let detachedOpeningProbe (text: string) =
        match workOf text with
        | Error error -> box {| decision = "Refuse"; detail = error; names = "" |}
        | Ok work ->
            match LifecycleKernel.refuseDetachedOpening work with
            | KernelDecision.Write(events, _) ->
                box
                    {| decision = "Write"
                       detail = "opened"
                       names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat "," |}
            | KernelDecision.Refuse reason -> box {| decision = "Refuse"; detail = reason; names = "" |}
            | KernelDecision.Done label -> box {| decision = "Done"; detail = label; names = "" |}

    let private heldStore (store: obj) : IEventStore = unbox store

    let private workOfStore (held: IEventStore) (workId: string) : LifecycleWork option =
        LifecycleKernel.tryWorkOfStore held workId

    let private commitEvents (held: IEventStore) (workId: string) (events: LifecycleEvent list) : Task<Result<unit, string>> =
        task {
            match! LifecycleKernel.commit held workId events with
            | Ok CommitOutcome.AppendDurable -> return Ok()
            | Ok CommitOutcome.AppendNotAttempted -> return Error "append was not attempted"
            | Ok CommitOutcome.CommitUnknown -> return Error "commit outcome is unknown"
            | Error reason -> return Error reason
        }

    let private openingKindOf (kind: string) =
        match kind with
        | "S4" -> OpeningKind.ManagerDirect
        | "spawn" -> OpeningKind.SpawnedExecution
        | _ -> OpeningKind.UserPlan

    let workView (store: obj) (workId: string) =
        let held = heldStore store

        match workOfStore held workId with
        | None -> box {| found = false; sequence = ""; active = false; reviewed = ""; confirmed = false; delivered = false |}
        | Some work ->
            let reviewed =
                work.Active
                |> Option.bind (fun active -> active.Review)
                |> Option.map (fun stamp -> ReviewOutcome.render stamp.Outcome)
                |> Option.defaultValue ""

            box
                {| found = true
                   sequence = WorkSequence.render work.Sequence |> String.concat ","
                   active = work.Active.IsSome
                   reviewed = reviewed
                   confirmed =
                    work.Active
                    |> Option.bind (fun active -> active.Confirmed)
                    |> Option.isSome
                   delivered = work.CurrentDelivered |}

    let bindDevOps (store: obj) (workId: string) (devopsId: string) (target: obj) : Task<obj> =
        task {
            let held = heldStore store
            let targetText = if isNull target then None else Some(string target)

            match workOfStore held workId with
            | None -> return box {| ok = false; error = "lifecycle work is missing" |}
            | Some work ->
                match LifecycleKernel.bindDevOps work BindTrigger.Ask devopsId targetText with
                | KernelDecision.Refuse reason -> return box {| ok = false; error = reason |}
                | KernelDecision.Done _ -> return box {| ok = true; error = "" |}
                | KernelDecision.Write(events, _) ->
                    match! commitEvents held workId events with
                    | Ok() -> return box {| ok = true; error = "" |}
                    | Error reason -> return box {| ok = false; error = reason |}
        }

    let roadDevOps (store: obj) (workId: string) =
        match workOfStore (heldStore store) workId |> Option.bind (fun work -> work.DevOps) with
        | None -> null
        | Some(devopsId, target) ->
            box
                {| devopsId = devopsId
                   modelTarget = target |> Option.toObj |}
