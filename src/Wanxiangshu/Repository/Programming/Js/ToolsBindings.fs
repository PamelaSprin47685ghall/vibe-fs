namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open System.Text
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity

/// JS-010/JS-016: the api object injected into the sandbox — the only
/// authority a model program sees. Every member returns a JSON-compatible
/// object; failures carry `{ ok: false, code, reason }` with stable codes
/// (JS-019). Mutations write as-is: each edit/write is atomically persisted
/// through MutationFs at call time; the staging buffer records what was
/// actually written, so a later failure keeps earlier writes on disk (JS-012).
module JsToolsBindings =

    [<Import("resolve", "node:path")>]
    let private pathResolve (path: string) : string = jsNative

    [<Import("relative", "node:path")>]
    let private pathRelative (from: string) (toPath: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("isAbsolute", "node:path")>]
    let private pathIsAbsolute (path: string) : bool = jsNative

    [<Import("realpathSync", "node:fs")>]
    let private realpathSync (path: string) : string = jsNative

    [<Emit("$0 === undefined || $0 === null")>]
    let private isUndefined (value: obj) : bool = jsNative

    let internal failureObj (failure: JsFailure) : obj =
        createObj
            [ "ok" ==> false
              "code" ==> JsFailure.code failure
              "reason" ==> JsFailure.reason failure ]

    [<Emit("new Error().stack")>]
    let private currentStack () : string = jsNative

    /// `js.call` returns a structured outcome; a host failure must surface as a
    /// decoded js failure, so reject the promise with the `__jsFailure`
    /// sentinel the wrapper classifies instead of returning ok:false as data.
    [<Emit("Promise.resolve($0).then(function(value) { if (value && value.ok === false) { const err = new Error(value.reason || value.code); err.__jsFailure = { code: value.code, reason: value.reason || value.code, minimumOutputBudgetBytes: value.minimumOutputBudgetBytes }; throw err; } return value; })")>]
    let private rejectRunFailure (promise: obj) : obj = jsNative

    /// A span must cross the vm boundary as a plain JS array, not a typed
    /// array: the parked payload is JSON round-tripped, and a typed array
    /// serializes to an object with numeric keys.
    [<Emit("[$0, $1]")>]
    let private jsSpan (beginIndex: int) (endIndex: int) : obj = jsNative

    let private renderOutcome (outcome: Result<obj, JsFailure>) : obj =
        match outcome with
        | Error failure -> failureObj failure
        | Ok value -> value

    /// Path boundary: a path is legal iff its resolved form stays inside root.
    /// Absolute paths are allowed when they resolve inside root; anything else
    /// is PATH_DENIED (JS-007 capability boundary).
    let private resolveInside (root: string) (path: string) : Result<string, JsFailure> =
        let full =
            if System.String.IsNullOrEmpty path then
                pathResolve root
            else
                pathResolve (pathJoin root path)

        let rel = pathRelative root full

        if rel = "" || (not (rel.StartsWith "..") && not (pathIsAbsolute rel)) then
            Ok full
        else
            Error(JsFailure.PathDenied path)

    let private readPathResolutionFailure path (error: obj) =
        if string error?code = "ENOENT" then
            JsFailure.FileNotFound path
        else
            JsFailure.FileReadFailed path

    let private resolveReadPath path =
        try
            Ok(realpathSync path)
        with error ->
            Error(readPathResolutionFailure path error)

    /// Symlink-escape check for an existing path: resolve both ends and
    /// require the target to stay inside the capability boundary.
    let private ensureRealPathInside (root: string) (callerPath: string) (full: string) =
        result {
            let! readPath = resolveReadPath full
            let! readRoot = resolveReadPath root
            let relative = (pathRelative readRoot readPath).Replace("\\", "/")

            do!
                if relative = ".." || relative.StartsWith("../") || pathIsAbsolute relative then
                    Error(JsFailure.PathDenied callerPath)
                else
                    Ok()
        }


    let createLedger (persistence: IJsTransactionPersistence option) : JsAsIsLedger =
        { Persistence = persistence
          Appends = ResizeArray() }

    /// Observe a fired append so a rejected task never becomes an unhandled
    /// rejection on a path that does not settle the ledger (a parked site
    /// suspends before settlement).
    [<Emit("$0.catch(() => undefined)")>]
    let private observeRejection (append: obj) : obj = jsNative

    /// H1: fire the Prepared append before the write lands. Returns the
    /// transaction id so the matching Committed can be fired after the write;
    /// a failed write leaves that Prepared as interrupt evidence ([015]).
    let private firePrepared
        (ledger: JsAsIsLedger)
        (root: string)
        (mutation: JsStagedMutation)
        : JsTransactionId option =
        match ledger.Persistence with
        | None -> None
        | Some durable ->
            let prepared =
                { TransactionId = JsTransactionId.generate ()
                  WorkspaceRoot = root
                  Mutation = JsTransactionFacts.ofMutation mutation }

            let pending = durable.AppendPrepared prepared
            observeRejection (box pending) |> ignore
            ledger.Appends.Add pending
            Some prepared.TransactionId

    /// H1: fire the Committed append after the write landed.
    let private fireCommitted (ledger: JsAsIsLedger) (transactionId: JsTransactionId option) : unit =
        match ledger.Persistence, transactionId with
        | Some durable, Some transactionId ->
            let pending = durable.AppendCommitted transactionId
            observeRejection (box pending) |> ignore
            ledger.Appends.Add pending
        | _ -> ()


    /// JS-020/JS-030 write-before check: a mutation target that was read
    /// through file() or cat() must still match its snapshot. A third-party
    /// change is FILE_CHANGED with no implicit retry.
    let private checkSnapshotFreshness
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (path: string)
        (full: string)
        : Result<unit, JsFailure> =
        readSnapshots
        |> Seq.tryFind (fun snapshot -> snapshot.Path = path)
        |> function
            | None -> Ok()
            | Some snapshot ->
                match JsUtf8Fs.readUtf8Classified full with
                | Ok current when current = snapshot.Text -> Ok()
                | _ -> Error(JsFailure.FileChanged path)

    let private makeWriteMember
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (ledger: JsAsIsLedger)
        =
        "write"
        ==> fun (path: string) (text: string) ->
            result {
                let! full = resolveInside root path
                do! checkSnapshotFreshness readSnapshots path full

                let! mutation =
                    if JsMutationFs.existsPath full then
                        JsUtf8Fs.readUtf8Classified full
                        |> Result.map (fun current -> JsStagedMutation.Rewrite(path, current, text))
                    else
                        Ok(JsStagedMutation.Create(path, text))

                // As-is write: overwrite an existing file or create a missing
                // one in one atomic mutation. A rewrite carries expectedCurrent
                // so a third-party change since the read is FILE_CHANGED; a
                // create is no-clobber.
                // H1: Prepared fires before the write, Committed right after.
                let transactionId = firePrepared ledger root mutation
                do! JsMutationFs.commitPlan root (JsTransaction.commitPlan [ mutation ])
                fireCommitted ledger transactionId
                staging.Add mutation
                return createObj [ "ok" ==> true ]
            }
            |> renderOutcome

    /// The rm/mv primitives carry the resolved absolute path in their
    /// failures; the member contract reports the caller-visible path (WHAT
    /// [018]: reasons never leak host paths).
    let private withCallerPath (path: string) (failure: JsFailure) =
        match failure with
        | JsFailure.DirectoryNotEmpty _ -> JsFailure.DirectoryNotEmpty path
        | JsFailure.FileNotFound _ -> JsFailure.FileNotFound path
        | JsFailure.FileReadFailed _ -> JsFailure.FileReadFailed path
        | JsFailure.CrossDeviceMove _ -> JsFailure.CrossDeviceMove path
        | other -> other

    /// JS-020/JS-030 rm: delete a file or an empty directory; refuse a
    /// non-empty directory (DIRECTORY_NOT_EMPTY). Missing target is
    /// FILE_NOT_FOUND. Takes effect immediately (as-is) and records one
    /// durable Remove entry: H1 fires Prepared before the deletion and
    /// Committed right after it landed (per-mutation granularity, no
    /// multi-mutation aggregation).
    let private makeRmMember
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (ledger: JsAsIsLedger)
        =
        "rm"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path

                if not (JsMutationFs.existsPath full) then
                    return! Error(JsFailure.FileNotFound path)

                do! checkSnapshotFreshness readSnapshots path full

                let transactionId = firePrepared ledger root (JsStagedMutation.Remove path)
                do! (JsMutationFs.removePath full |> Result.mapError (withCallerPath path))
                fireCommitted ledger transactionId

                staging.Add(JsStagedMutation.Remove path)
                return createObj [ "ok" ==> true; "removed" ==> path ]
            }
            |> renderOutcome

    /// JS-020/JS-030 mv: move or rename a file or directory (POSIX mv).
    /// Overwrite is allowed; a missing source is FILE_NOT_FOUND. Both sides
    /// resolve inside the capability boundary. A cross-device move (EXDEV) is
    /// refused: renameSync is atomic, so on EXDEV neither side changed and
    /// no two-sided intermediate state is ever left behind. H1 fires Prepared
    /// before the move and Committed right after it landed (same ledger seam
    /// as makeRmMember — the members land the as-is effect and fire the
    /// per-mutation accounting themselves).
    let private makeMvMember
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (ledger: JsAsIsLedger)
        =
        "mv"
        ==> fun (fromPath: string) (toPath: string) ->
            result {
                let! fromFull = resolveInside root fromPath
                let! toFull = resolveInside root toPath

                if not (JsMutationFs.existsPath fromFull) then
                    return! Error(JsFailure.FileNotFound fromPath)

                do! checkSnapshotFreshness readSnapshots fromPath fromFull

                if JsMutationFs.existsPath toFull then
                    do! checkSnapshotFreshness readSnapshots toPath toFull

                let transactionId =
                    firePrepared ledger root (JsStagedMutation.Move(fromPath, toPath))

                do!
                    (JsMutationFs.movePath fromFull toFull
                     |> Result.mapError (withCallerPath fromPath))

                fireCommitted ledger transactionId

                staging.Add(JsStagedMutation.Move(fromPath, toPath))
                return createObj [ "ok" ==> true; "moved" ==> fromPath; "destination" ==> toPath ]
            }
            |> renderOutcome

    /// cat(path): strict-UTF-8 snapshot read. Registers the snapshot for the
    /// write-before FILE_CHANGED check and the explicit-read case record.
    let private makeCatMember
        (root: string)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        =
        "cat"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path
                let! readPath = resolveReadPath full
                let! readRoot = resolveReadPath root
                let relative = (pathRelative readRoot readPath).Replace("\\", "/")

                do!
                    if relative = ".." || relative.StartsWith("../") || pathIsAbsolute relative then
                        Error(JsFailure.PathDenied path)
                    else
                        Ok()

                let! text = JsUtf8Fs.readUtf8Classified readPath
                readSnapshots.Add { Path = path; Text = text }
                explicitReads.Add { Path = readPath; ResultBytes = text }

                return createObj [ "ok" ==> true; "text" ==> text ]
            }
            |> renderOutcome

    /// ls(path): one directory, sorted, directory entries end with "/".
    let private makeLsMember (root: string) =
        "ls"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path
                do! ensureRealPathInside root path full
                let! entries = JsGlobFs.visibleEntries root path

                let names =
                    entries
                    |> List.map (fun entry -> if entry.IsDir then entry.Name + "/" else entry.Name)
                    |> List.toArray

                return createObj [ "ok" ==> true; "entries" ==> names ]
            }
            |> renderOutcome

    /// stat(path): metadata; a missing path is a normal `exists = false`.
    let private makeStatMember (root: string) =
        "stat"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path
                let! stat = JsMutationFs.statPath full

                do!
                    if unbox<bool> (stat?exists) then
                        ensureRealPathInside root path full
                    else
                        Ok()

                return createObj [ "ok" ==> true; "stat" ==> stat ]
            }
            |> renderOutcome

    /// mkdir(path): POSIX non-recursive directory creation. Structural, so it
    /// carries no durable mutation entry (the as-is ledger owns file writes,
    /// removals and moves).
    let private makeMkdirMember (root: string) =
        "mkdir"
        ==> fun (path: string) ->
            result {
                let! full = resolveInside root path
                do! JsMutationFs.makeDirectory full
                return createObj [ "ok" ==> true; "path" ==> path ]
            }
            |> renderOutcome

    let private isWhitespaceChar (ch: char) =
        ch = ' ' || ch = '\t' || ch = '\n' || ch = '\r' || ch = '\012' || ch = '\011'

    let private appendPendingSpace (sb: StringBuilder) (pendingSpace: bool) =
        if pendingSpace && sb.Length > 0 then
            sb.Append(' ') |> ignore

    let private accumulateCollapsedChar (sb: StringBuilder) (pendingSpace: bool) (ch: char) =
        if isWhitespaceChar ch then
            true
        else
            appendPendingSpace sb pendingSpace
            sb.Append(ch) |> ignore
            false

    /// Collapse a whitespace run to one space and drop leading/trailing
    /// whitespace. This is the canonical `where` needle normalization.
    let private collapseWhitespace (text: string) =
        let sb = StringBuilder()
        // DSL-MUTABLE: algorithm-scratch — whitespace run cursor
        let mutable pendingSpace = false

        for ch in text do
            pendingSpace <- accumulateCollapsedChar sb pendingSpace ch

        sb.ToString()

    /// Normalize content and keep the original offset of every normalized
    /// character. A whitespace run maps to its first original character.
    let private appendNormalizedSpace
        (sb: StringBuilder)
        (offsets: ResizeArray<int>)
        (previousWasSpace: bool)
        (index: int)
        =
        if not previousWasSpace && sb.Length > 0 then
            sb.Append(' ') |> ignore
            offsets.Add index
            true
        else
            previousWasSpace

    let private accumulateNormalizedChar
        (sb: StringBuilder)
        (offsets: ResizeArray<int>)
        (previousWasSpace: bool)
        (index: int)
        (ch: char)
        =
        if isWhitespaceChar ch then
            appendNormalizedSpace sb offsets previousWasSpace index
        else
            sb.Append ch |> ignore
            offsets.Add index
            false

    let private normalizeWithMap (text: string) =
        let sb = StringBuilder()
        let offsets = ResizeArray<int>()
        // DSL-MUTABLE: algorithm-scratch — whitespace run cursor
        let mutable previousWasSpace = false

        for index in 0 .. text.Length - 1 do
            let ch = text.[index]
            previousWasSpace <- accumulateNormalizedChar sb offsets previousWasSpace index ch

        sb.ToString(), offsets

    /// All non-overlapping normalized matches, mapped back to original
    /// UTF-16 offsets. `fromIndex` is an original content offset.
    let private recordSpanMatch
        (offsets: ResizeArray<int>)
        (spans: ResizeArray<int * int>)
        (targetLength: int)
        (fromIndex: int)
        (found: int)
        =
        let beginIndex = offsets.[found]
        let endIndex = offsets.[found + targetLength - 1] + 1

        if beginIndex >= fromIndex then
            spans.Add(beginIndex, endIndex)

    let private scanNextSpan
        (normalized: string)
        (target: string)
        (offsets: ResizeArray<int>)
        (spans: ResizeArray<int * int>)
        (fromIndex: int)
        (cursor: int)
        =
        let found = normalized.IndexOf(target, cursor)

        if found < 0 then
            normalized.Length
        else
            recordSpanMatch offsets spans target.Length fromIndex found
            found + max 1 target.Length

    let private collectWhereSpans (content: string) (target: string) (fromIndex: int) =
        let normalized, offsets = normalizeWithMap content
        // DSL-MUTABLE: algorithm-scratch — match scan cursor
        let mutable cursor = 0
        let spans = ResizeArray<int * int>()

        while cursor <= normalized.Length - target.Length do
            cursor <- scanNextSpan normalized target offsets spans fromIndex cursor

        List.ofSeq spans

    let private whereSpans (content: string) (needle: string) (fromIndex: int) =
        let target = collapseWhitespace needle

        if target.Length = 0 then
            []
        else
            collectWhereSpans content target fromIndex

    let private computeRowCell (aChar: char) (bChar: char) (leftDist: int) (aboveDist: int) (diagDist: int) =
        let substitution = if aChar = bChar then 0 else 1
        min (min (leftDist + 1) (aboveDist + 1)) (diagDist + substitution)

    let private computeDistanceRow (aChar: char) (b: string) (i: int) (previous: int[]) (current: int[]) =
        current.[0] <- i

        for j in 1 .. b.Length do
            current.[j] <- computeRowCell aChar b.[j - 1] current.[j - 1] previous.[j] previous.[j - 1]

        Array.blit current 0 previous 0 current.Length

    let private editDistance (left: string) (right: string) =
        let a = if left.Length > 200 then left.Substring(0, 200) else left

        let b =
            if right.Length > 200 then
                right.Substring(0, 200)
            else
                right

        let previous = Array.init (b.Length + 1) id
        let current = Array.zeroCreate<int> (b.Length + 1)

        for i in 1 .. a.Length do
            computeDistanceRow a.[i - 1] b i previous current

        previous.[b.Length]

    type private NearestCandidate =
        { Distance: int
          Start: int
          End: int
          Text: string }

    let private considerBetterCandidate
        (currentBest: NearestCandidate)
        (distance: int)
        (start: int)
        (endOffset: int)
        (text: string)
        =
        if distance < currentBest.Distance then
            { Distance = distance
              Start = start
              End = endOffset
              Text = text }
        else
            currentBest

    let private scanLineCandidate
        (target: string)
        (offset: int)
        (line: string)
        (scanned: int)
        (best: NearestCandidate)
        =
        if scanned >= 2000 || System.String.IsNullOrWhiteSpace line then
            scanned, best
        else
            let trimmed = line.Trim()
            let distance = editDistance trimmed target
            scanned + 1, considerBetterCandidate best distance offset (offset + line.Length) trimmed

    let private scanNearestLine (content: string) (target: string) =
        let lines = content.Split('\n')
        // DSL-MUTABLE: algorithm-scratch — nearest-line scan state
        let mutable offset = 0
        let mutable scanned = 0

        // DSL-MUTABLE: algorithm-scratch — nearest candidate so far
        let mutable best =
            { Distance = System.Int32.MaxValue
              Start = 0
              End = 0
              Text = "" }

        for line in lines do
            let nextScanned, nextBest = scanLineCandidate target offset line scanned best
            scanned <- nextScanned
            best <- nextBest
            offset <- offset + line.Length + 1

        best

    let private renderNearestMatch (best: NearestCandidate) : obj =
        if best.Distance = System.Int32.MaxValue then
            null
        else
            createObj
                [ "span" ==> jsSpan best.Start best.End
                  "text" ==> best.Text
                  "distance" ==> best.Distance ]

    /// Diagnostic for a zero-match where: the closest single line, bounded to
    /// the first 2000 non-empty lines so a huge file cannot dominate cost.
    let private nearestLine (content: string) (needle: string) =
        let target = collapseWhitespace needle

        if target.Length = 0 then
            null
        else
            renderNearestMatch (scanNearestLine content target)

    /// Shared suspension path: park(payload) and where ambiguity both fire
    /// the same signal and await the same deferred promise.
    let private suspend (parkState: Wanxiangshu.Process.JsSandbox.ParkState) (payload: obj) : obj =
        if parkState.AbandonRequested then
            failureObj JsFailure.CannotParkWhileAbandoning
        else
            parkState.ParkStack <- currentStack ()
            Wanxiangshu.Process.JsSandbox.suspendProgram parkState payload

    let private candidateObjects (content: string) (spans: (int * int) list) =
        spans
        |> List.truncate 8
        |> List.map (fun (beginIndex, endIndex) ->
            let contextStart = max 0 (beginIndex - 80)
            let contextEnd = min content.Length (endIndex + 80)

            createObj
                [ "span" ==> jsSpan beginIndex endIndex
                  "context" ==> content.Substring(contextStart, contextEnd - contextStart) ])
        |> Array.ofList

    let private resolveStartIndex (fromIndex: obj) =
        if isUndefined fromIndex then
            0
        else
            max 0 (int (unbox<float> fromIndex))

    let private assignParkStack (state: Wanxiangshu.Process.JsSandbox.ParkState) (stack: obj) =
        if not (isNull stack) then
            state.ParkStack <- string stack

    let private whereNearestDiagnostic (content: string) (needle: string) (spans: (int * int) list) : obj =
        if List.isEmpty spans then
            nearestLine content needle
        else
            null

    let private suspendAmbiguity
        (state: Wanxiangshu.Process.JsSandbox.ParkState)
        (needle: string)
        (content: string)
        (spans: (int * int) list)
        (stack: obj)
        =
        assignParkStack state stack

        let payload =
            createObj
                [ "needle" ==> needle
                  "candidates" ==> candidateObjects content spans
                  "nearest" ==> whereNearestDiagnostic content needle spans ]

        suspend state payload

    let private parkOrRejectAmbiguity
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (needle: string)
        (content: string)
        (spans: (int * int) list)
        (stack: obj)
        =
        match parkState with
        | None -> failureObj (JsFailure.InvalidArgument "where requires a park-capable surface")
        | Some state -> suspendAmbiguity state needle content spans stack

    let private dispatchWhereOutcome
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (needle: string)
        (content: string)
        (spans: (int * int) list)
        (stack: obj)
        =
        match spans with
        | [ beginIndex, endIndex ] -> createObj [ "ok" ==> true; "span" ==> jsSpan beginIndex endIndex ]
        | _ -> parkOrRejectAmbiguity parkState needle content spans stack

    /// whereAll(content, needle, from?): every normalized match as [start, end].
    let private makeWhereAllMember () =
        "whereAll"
        ==> fun (content: string) (needle: string) (fromIndex: obj) ->
            if isNull content || isNull needle then
                failureObj (JsFailure.InvalidArgument "whereAll requires string content and needle")
            else
                let start = resolveStartIndex fromIndex
                let spans = whereSpans content needle start

                createObj
                    [ "ok" ==> true
                      "spans"
                      ==> (spans
                           |> List.map (fun (beginIndex, endIndex) -> jsSpan beginIndex endIndex)
                           |> Array.ofList) ]

    /// where(content, needle, from?): exactly one match returns its span;
    /// zero or many suspend the program through the same park channel as
    /// js.park, with a diagnostic payload. The injected value is returned
    /// verbatim (Unix responsibility: a wrong injection fails downstream).
    let private makeWhereMember (parkState: Wanxiangshu.Process.JsSandbox.ParkState option) =
        "where"
        ==> fun (content: string) (needle: string) (fromIndex: obj) (stack: obj) ->
            if isNull content || isNull needle then
                failureObj (JsFailure.InvalidArgument "where requires string content and needle")
            else
                let start = resolveStartIndex fromIndex
                let spans = whereSpans content needle start
                dispatchWhereOutcome parkState needle content spans stack

    /// PLAN S2 step 4: the park member. `await js.park(payload)` suspends the
    /// program at this await point through the shared suspension path.
    let private makeParkMember (parkState: Wanxiangshu.Process.JsSandbox.ParkState) =
        "park" ==> fun (payload: obj) -> suspend parkState payload

    [<Emit("Promise.reject(Object.assign(new Error($1), { __jsFailure: { code: $0, reason: $1 } }))")>]
    let private rejectFailure (code: string) (reason: string) : obj = jsNative

    let private finishAsked state text stack asked =
        match asked with
        | Error reason ->
            let failure = JsFailure.AskRefused reason
            rejectFailure (JsFailure.code failure) (JsFailure.reason failure)
        | Ok() ->
            assignParkStack state stack
            suspend state (createObj [ "question" ==> text ])

    let makeAskMember (state: Wanxiangshu.Process.JsSandbox.ParkState) (invoke: string -> Task<Result<unit, string>>) =
        "ask"
        ==> fun (question: obj) (stack: obj) ->
            let text =
                if emitJsExpr question "typeof $0 === 'string'" then
                    string question
                else
                    ""

            let fail failure =
                rejectFailure (JsFailure.code failure) (JsFailure.reason failure)

            if System.String.IsNullOrWhiteSpace text then
                fail (JsFailure.InvalidArgument "ask requires a question")
            elif state.AbandonRequested then
                fail JsFailure.CannotParkWhileAbandoning
            else
                task {
                    let! asked = invoke text
                    return finishAsked state text stack asked
                }
                |> box

    let scopePlanFile (root: string) (canWrite: unit -> bool) (api: obj) : obj =
        let source = api

        let samePhysicalPath path full realFile =
            if realFile = full then
                Ok()
            else
                Error(JsFailure.PathDenied path)

        let checkExisting path full =
            if JsMutationFs.existsPath full then
                resolveReadPath full |> Result.bind (samePhysicalPath path full)
            else
                Ok()

        let checkPath (path: string) =
            result {
                let! full = resolveInside root path

                if full <> pathResolve (pathJoin root "plan.md") then
                    return! Error(JsFailure.PathDenied path)

                let! realRoot = resolveReadPath root

                if realRoot <> pathResolve root then
                    return! Error(JsFailure.PathDenied path)

                do! checkExisting path full
            }

        let read name =
            name
            ==> fun (path: string) ->
                match checkPath path with
                | Error failure -> box (Task.FromResult(failureObj failure))
                | Ok() -> emitJsExpr (source, name, path) "$0[$1]($2)"

        createObj
            [ read "cat"
              read "stat"
              "where" ==> source?where
              "whereAll" ==> source?whereAll
              "park" ==> source?park
              "ask" ==> source?ask
              "write"
              ==> fun (path: string) (text: string) ->
                  match checkPath path with
                  | Error failure -> failureObj failure
                  | Ok() when not (canWrite ()) -> failureObj JsFailure.PlanReviewRequired
                  | Ok() when System.String.IsNullOrWhiteSpace text ->
                      failureObj (JsFailure.InvalidArgument "Plan content cannot be empty")
                  | Ok() -> emitJsExpr (source, path, text) "$0.write($1,$2)" ]

    /// The host loop behind `js.call`, injected by the workflow.
    type JsCallHost =
        { Loop: Wanxiangshu.Process.JsSandbox.ParkState option -> obj -> string -> Task<obj> }

    type JsSearchHost =
        { Search: string array -> bool -> Task<obj> }

    type private RgArguments =
        { Operands: string list
          HasPattern: bool
          FilesOnly: bool
          PathsOnly: bool }

    let private rgPath root path =
        if pathIsAbsolute path then
            ensureRealPathInside root path (pathResolve path)
        else
            result {
                let! full = resolveInside root path
                return! ensureRealPathInside root path full
            }

    let private rgFlag key parsed =
        match key with
        | "--files" ->
            Ok
                { parsed with
                    FilesOnly = true
                    PathsOnly = true }
        | "-l"
        | "--files-with-matches"
        | "--files-without-match" -> Ok { parsed with PathsOnly = true }
        | "-i"
        | "--ignore-case"
        | "-s"
        | "--case-sensitive"
        | "-S"
        | "--smart-case"
        | "-F"
        | "--fixed-strings"
        | "-w"
        | "--word-regexp"
        | "-x"
        | "--line-regexp"
        | "-n"
        | "--line-number"
        | "-N"
        | "--no-line-number"
        | "--hidden"
        | "--no-ignore"
        | "--no-ignore-vcs"
        | "--json" -> Ok parsed
        | _ -> Error(JsFailure.InvalidArgument("unsupported rg option: " + key))

    let private rgValue root key value parsed =
        match key with
        | "-e"
        | "--regexp" -> Ok { parsed with HasPattern = true }
        | "-f"
        | "--file" -> rgPath root value |> Result.map (fun () -> { parsed with HasPattern = true })
        | "--ignore-file" -> rgPath root value |> Result.map (fun () -> parsed)
        | _ -> Ok parsed

    let private rgValueOptions =
        set
            [ "-e"
              "--regexp"
              "-f"
              "--file"
              "-g"
              "--glob"
              "--iglob"
              "-t"
              "--type"
              "-T"
              "--type-not"
              "-m"
              "--max-count"
              "--max-depth"
              "--encoding"
              "--ignore-file" ]

    let private rgArguments root (args: string array) =
        let rec scan index afterOptions parsed =
            if index >= args.Length then
                Ok parsed
            else
                tokenAt index afterOptions parsed

        and tokenAt index afterOptions parsed =
            let token = args[index]

            if not afterOptions && token = "--" then
                scan (index + 1) true parsed
            elif not afterOptions && token.StartsWith("-") then
                optionAt index token parsed
            else
                scan
                    (index + 1)
                    afterOptions
                    { parsed with
                        Operands = token :: parsed.Operands }

        and optionAt index (token: string) parsed =
            let separator = token.IndexOf '='

            let key =
                if separator < 0 then
                    token
                else
                    token.Substring(0, separator)

            if Set.contains key rgValueOptions then
                valueAt index separator key token parsed
            elif separator >= 0 then
                Error(JsFailure.InvalidArgument("unsupported rg option: " + token))
            else
                rgFlag key parsed |> Result.bind (scan (index + 1) false)

        and valueAt index separator key token parsed =
            if separator >= 0 then
                rgValue root key (token.Substring(separator + 1)) parsed
                |> Result.bind (scan (index + 1) false)
            elif index + 1 < args.Length then
                rgValue root key args[index + 1] parsed |> Result.bind (scan (index + 2) false)
            else
                Error(JsFailure.InvalidArgument("rg option requires a value: " + key))

        result {
            let! parsed =
                scan
                    0
                    false
                    { Operands = []
                      HasPattern = false
                      FilesOnly = false
                      PathsOnly = false }

            let operands = List.rev parsed.Operands

            let paths =
                if parsed.HasPattern || parsed.FilesOnly then
                    operands
                else
                    operands |> List.skip (min 1 operands.Length)

            do!
                if
                    List.isEmpty paths
                    || (not parsed.HasPattern && not parsed.FilesOnly && operands.Length < 2)
                then
                    Error(
                        JsFailure.InvalidArgument
                            "rg requires a pattern and an explicit search path, or --files with a path"
                    )
                else
                    paths |> List.traverseResultM (rgPath root) |> Result.map ignore

            let framing = if parsed.PathsOnly then "--null" else "--json"
            return Array.append [| "--no-config"; "--no-require-git"; framing |] args, parsed.PathsOnly
        }

    [<Emit("Array.isArray($0) && Array.prototype.every.call($0, value => typeof value === 'string' && !value.includes('\\0'))")>]
    let private isSearchArguments (value: obj) : bool = jsNative

    let private invokeRg root arguments host =
        match rgArguments root arguments, host with
        | Error failure, _ -> Task.FromResult(failureObj failure)
        | Ok(invocation, pathsOnly), Some injected -> injected.Search invocation pathsOnly
        | Ok _, None -> Task.FromResult(failureObj (JsFailure.RgFailed "search Host is unavailable"))

    let private makeRgMember root (host: JsSearchHost option) =
        "rg"
        ==> fun (argv: obj) ->
            let search =
                task {
                    if not (isSearchArguments argv) then
                        return failureObj (JsFailure.InvalidArgument "rg requires one string array argv without NUL")
                    else
                        let arguments: string array = emitJsExpr argv "Array.from($0)"

                        return! invokeRg root arguments host
                }

            rejectRunFailure (box search)

    /// `js.call(class extends js.Callable { ... })`: hand the class to the host
    /// loop and await its structured outcome. Without an injected host loop
    /// the member refuses with CALL_HOST_UNAVAILABLE instead of pretending.
    let private makeCallMember (host: JsCallHost option) (parkState: Wanxiangshu.Process.JsSandbox.ParkState option) =
        let settled =
            fun (runnableClass: obj) ->
                match host with
                | Some injected -> box (injected.Loop parkState runnableClass (currentStack ()))
                | None -> box (task { return failureObj JsFailure.RunHostUnavailable })

        "call" ==> fun (callableClass: obj) -> rejectRunFailure (settled callableClass)

    /// `js.fork(payload)` hands the payload to the wiring-owned fork spec and
    /// returns its receipt text; `js.join()` waits for this session's children.
    /// Without an installed delegation host the members refuse with
    /// DELEGATION_UNAVAILABLE instead of pretending.
    let private makeForkMember (host: JsDelegationHost option) =
        "fork"
        ==> fun (payload: obj) ->
            match host with
            | Some delegation -> box (delegation.Fork payload)
            | None -> box (task { return failureObj JsFailure.DelegationUnavailable })

    let private makeCommissionMember (host: JsDelegationHost option) =
        let _, fork = makeForkMember host
        "commission", fork

    let private makeJoinMember (host: JsDelegationHost option) =
        "join"
        ==> fun () ->
            match host with
            | Some delegation -> box (delegation.Join())
            | None -> box (task { return failureObj JsFailure.DelegationUnavailable })

    let private makeFissionMember (host: JsDelegationHost option) =
        "fission"
        ==> fun (payload: obj) ->
            match host with
            | Some delegation -> box (delegation.Fission payload)
            | None -> box (task { return failureObj JsFailure.DelegationUnavailable })

    /// `js.horizon()` returns the horizon roster as a data envelope (who is
    /// still away, each participant's status/workState/record and the four
    /// calibration numbers). Without an installed delegation host the member
    /// refuses with DELEGATION_UNAVAILABLE instead of pretending.
    let makeHorizonMember (host: JsDelegationHost option) : (string * obj) =
        "horizon"
        ==> fun () ->
            match host with
            | Some delegation -> box (delegation.Horizon())
            | None -> box (task { return failureObj JsFailure.DelegationUnavailable })

    /// `js.advisor(intent, view)` consults this session's own advisor seat and
    /// returns its data envelope verbatim: the advice travels as the value the
    /// host closure produced, never as a rendering. Without an installed
    /// delegation host the member refuses with DELEGATION_UNAVAILABLE instead
    /// of pretending.
    let makeAdvisorMember (host: JsDelegationHost option) : (string * obj) =
        "advisor"
        ==> fun (intent: obj) (view: obj) ->
            match host with
            | Some delegation -> box (delegation.Advisor intent view)
            | None -> box (task { return failureObj JsFailure.DelegationUnavailable })

    let private makeAvailableAskMember parkState askInvoke =
        match parkState, askInvoke with
        | Some state, Some invoke -> makeAskMember state invoke
        | _ ->
            "ask"
            ==> fun (_: obj) (_: obj) ->
                rejectFailure "ASK_REFUSED" "this.ask has no user channel in this execution path"

    let private createApiCore
        (capabilities: Set<JsCapability>)
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        (ledger: JsAsIsLedger)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState option)
        (runHost: JsCallHost option)
        (searchHost: JsSearchHost option)
        (delegationHost: JsDelegationHost option)
        (askInvoke: (string -> Task<Result<unit, string>>) option)
        : obj =
        let members =
            [ if Set.contains JsCapability.Read capabilities then
                  makeCatMember root readSnapshots explicitReads
                  makeStatMember root
                  makeWhereAllMember ()
                  makeWhereMember parkState

              if Set.contains JsCapability.Glob capabilities then
                  makeLsMember root

              if Set.contains JsCapability.Grep capabilities then
                  makeRgMember root searchHost

              if Set.contains JsCapability.Write capabilities then
                  makeWriteMember root staging readSnapshots ledger
                  makeMkdirMember root

              if Set.contains JsCapability.Remove capabilities then
                  makeRmMember root staging readSnapshots ledger

              if Set.contains JsCapability.Move capabilities then
                  makeMvMember root staging readSnapshots ledger

              if Set.contains JsCapability.Exec capabilities then
                  makeCallMember runHost parkState

              if Set.contains JsCapability.Commission capabilities then
                  makeCommissionMember delegationHost

              if Set.contains JsCapability.Fork capabilities then
                  makeForkMember delegationHost

              if Set.contains JsCapability.Join capabilities then
                  makeJoinMember delegationHost

              if Set.contains JsCapability.Fission capabilities then
                  makeFissionMember delegationHost

              if Set.contains JsCapability.Horizon capabilities then
                  makeHorizonMember delegationHost

              if Set.contains JsCapability.Advisor capabilities then
                  makeAdvisorMember delegationHost

              if Set.contains JsCapability.Ask capabilities then
                  makeAvailableAskMember parkState askInvoke

              match parkState with
              | Some state -> makeParkMember state
              | None -> () ]

        createObj members

    /// Build the api object for one sandbox run. `staging` records every
    /// mutation that was actually persisted as-is; `ledger` is the H1
    /// per-mutation durable channel the mutation members fire into; the
    /// caller reports and settles both. The park member is absent here (no
    /// park state); use `createApiWithPark`.
    let createApi
        (capabilities: Set<JsCapability>)
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        (ledger: JsAsIsLedger)
        (runHost: JsCallHost option)
        (searchHost: JsSearchHost option)
        (delegationHost: JsDelegationHost option)
        (askInvoke: (string -> Task<Result<unit, string>>) option)
        : obj =
        createApiCore
            capabilities
            root
            staging
            readSnapshots
            explicitReads
            ledger
            None
            runHost
            searchHost
            delegationHost
            askInvoke

    /// Build the api object for one sandbox run that carries a park lifecycle
    /// state (PLAN S2 step 4). `parkState` is the mutable object shared with
    /// the sandbox wrapper; the generated `js.park` member suspends through it.
    let createApiWithPark
        (capabilities: Set<JsCapability>)
        (root: string)
        (staging: ResizeArray<JsStagedMutation>)
        (readSnapshots: ResizeArray<JsReadSnapshot>)
        (explicitReads: ResizeArray<JsExplicitFileRead>)
        (ledger: JsAsIsLedger)
        (parkState: Wanxiangshu.Process.JsSandbox.ParkState)
        (runHost: JsCallHost option)
        (searchHost: JsSearchHost option)
        (delegationHost: JsDelegationHost option)
        (askInvoke: (string -> Task<Result<unit, string>>) option)
        : obj =
        createApiCore
            capabilities
            root
            staging
            readSnapshots
            explicitReads
            ledger
            (Some parkState)
            runHost
            searchHost
            delegationHost
            askInvoke

module JsReviewOnly =
    let scopeReviewOnly (check: unit -> Task<bool>) (api: obj) : obj =
        let guarded name =
            let invoke: obj = api?(name)

            name
            ==> fun (argument: obj) ->
                task {
                    let! allowed = check ()

                    if not allowed then
                        return JsToolsBindings.failureObj JsFailure.ReviewOnly
                    else
                        return! unbox<Task<obj>> (emitJsExpr (invoke, argument) "Promise.resolve($0($1))")
                }

        let members = [ "cat"; "stat"; "ls"; "rg" ]
        let review = members |> List.map guarded |> createObj

        for name in members do
            emitJsExpr (api, name) "delete $0[$1]" |> ignore

        api?reviewOnly <- review
        api
