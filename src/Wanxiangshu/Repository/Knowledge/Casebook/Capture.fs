namespace Wanxiangshu.Repository.Knowledge.Casebook

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Repository.Programming.Js
open Thoth.Json
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Persistence.EventStore

/// Access tracker for substantive file access collection.
type AccessTracker() =
    let paths = HashSet<string>()

    member this.recordRead(path: string, contentHash: string) : unit = this.RecordRead(path, contentHash)
    member this.recordCreate(path: string) : unit = this.RecordCreate(path)
    member this.recordEdit(path: string) : unit = this.RecordEdit(path)
    member this.recordDelete(path: string) : unit = this.RecordDelete(path)
    member this.recordMove(source: string, destination: string) : unit = this.RecordMove(source, destination)
    member this.recordGrep(pattern: string, path: string) : unit = this.RecordGrep(pattern, path)
    member this.recordGlob(pattern: string) : unit = this.RecordGlob(pattern)

    member this.recordAttemptedMutation(path: string, committed: bool) : unit =
        this.RecordAttemptedMutation(path, committed)

    member this.getRelatedPaths() : string array = this.GetRelatedPaths() |> List.toArray

    member _.RecordRead(path: string, contentHash: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then
            paths.Add path |> ignore

    member _.RecordCreate(path: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then
            paths.Add path |> ignore

    member _.RecordEdit(path: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then
            paths.Add path |> ignore

    member _.RecordDelete(path: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then
            paths.Add path |> ignore

    member _.RecordMove(source: string, destination: string) : unit =
        if not (String.IsNullOrWhiteSpace source) then
            paths.Add source |> ignore

        if not (String.IsNullOrWhiteSpace destination) then
            paths.Add destination |> ignore

    member _.RecordGrep(pattern: string, path: string) : unit = ()
    member _.RecordGlob(pattern: string) : unit = ()

    member _.RecordAttemptedMutation(path: string, committed: bool) : unit =
        if committed && not (String.IsNullOrWhiteSpace path) then
            paths.Add path |> ignore

    member _.GetRelatedPaths() : string list = paths |> Seq.toList |> List.sort

/// CASE-003 / KR-003 / KR-014: typed observation capture and substantive access.
module CasebookCapture =

    type MaintenanceCapture =
        { DiffSummary: string
          TargetState: string }

    [<RequireQualifiedAccess>]
    type private StoredFileState =
        | Missing
        | Present of sha256: string * payload: PayloadRef

    type private CapturedFile =
        { Bytes: byte[]
          Text: string
          Sha256: string }

    /// Stable content fingerprint for FileRead observations (CASE-003).
    let contentHash (text: string) : string =
        if isNull text then "" else HostDigest.sha256Hex text

    let private text (value: obj) : string option =
        if isNull value || value = null then
            None
        else
            Some(string value)

    let private pathArg (args: obj) : string option =
        [ "path"; "filePath"; "file" ]
        |> List.tryPick (fun key ->
            let raw = args?(key)
            if isNull raw then None else Some(string raw))

    /// read: args.path + rendered output → FileRead (hash of the observed text).
    let ofReadExecution (args: obj) (output: string) : Observation option =
        match pathArg args with
        | Some path when not (System.String.IsNullOrWhiteSpace output) ->
            Some(Observation.FileRead(path, contentHash output))
        | _ -> None

    /// glob: output lines are the matched relative paths (rendered one per
    /// line); pattern comes from args (pattern / glob / query, best-effort).
    let ofGlobExecution (args: obj) (output: string) : Observation option =
        let pattern =
            [ "pattern"; "glob"; "query" ]
            |> List.tryPick (fun key ->
                let raw = args?(key)
                if isNull raw then None else Some(string raw))

        let paths =
            output.Split '\n'
            |> Array.map (fun line -> line.Trim())
            |> Array.filter (fun line -> line <> "")
            |> Array.toList

        match pattern with
        | Some p when not (System.String.IsNullOrWhiteSpace p) -> Some(Observation.GlobResult(p, paths))
        | _ -> None

    /// grep: pattern from args; matches rendered as "path:line:index:text"
    /// lines — parse best-effort, keep the raw text for the match payload.
    let ofGrepExecution (args: obj) (output: string) : Observation option =
        let pattern =
            [ "pattern"; "regex"; "query" ]
            |> List.tryPick (fun key ->
                let raw = args?(key)
                if isNull raw then None else Some(string raw))

        let matches =
            output.Split '\n'
            |> Array.map (fun line -> line.Trim())
            |> Array.filter (fun line -> line <> "")
            |> Array.mapi (fun i line -> "grep-output", i, line)
            |> Array.toList

        match pattern with
        | Some p when not (System.String.IsNullOrWhiteSpace p) -> Some(Observation.GrepResult(p, matches))
        | _ -> None

    /// Dispatch by tool name (CASE-003).
    let capture (toolName: string) (args: obj) (output: string) : Observation option =
        match toolName with
        | "read" -> ofReadExecution args output
        | "glob" -> ofGlobExecution args output
        | "grep" -> ofGrepExecution args output
        | _ -> None

    // ---- executor reading tolerance (§63) ---------------------------------

    /// Split a command line on whitespace, honoring single quotes (best-effort
    /// — this is typed command parsing, never transcript inference).
    let private tokenize (command: string) : string list =
        let rec go (chars: char list) (current: string) (inQuote: bool) (acc: string list) : string list =
            match chars with
            | [] -> List.rev (if current = "" then acc else current :: acc)
            | ''' :: rest -> go rest current (not inQuote) acc
            | c :: rest when (c = ' ' || c = '\t') && not inQuote ->
                go rest "" false (if current = "" then acc else current :: acc)
            | c :: rest -> go rest (current + string c) inQuote acc

        go (List.ofSeq command) "" false []

    /// §63 positives: single-file reads via cat/head/tail/sed (with or without
    /// option prefixes). `cat file | grep bar` still counts as reading `file`.
    let rec private firstReadFile (tokens: string list) : string option =
        match tokens with
        | [] -> None
        | "-n" :: value :: tail when value |> Seq.forall System.Char.IsDigit -> firstReadFile tail
        | "-n" :: tail -> firstReadFile tail
        | token :: tail when token.StartsWith "-" -> firstReadFile tail
        | file :: _ -> Some file

    let private sedReadFile (rest: string list) =
        match rest |> List.skipWhile (fun token -> token.StartsWith "-") with
        | _script :: file :: _ -> Some(Observation.FileRead(file, contentHash ""))
        | _ -> None

    let private dispatchExecTokens tokens : Observation option =
        match tokens with
        | [] -> None
        | "sh" :: _
        | "bash" :: _ -> None
        | "cat" :: rest
        | "head" :: rest
        | "tail" :: rest ->
            firstReadFile rest
            |> Option.map (fun file -> Observation.FileRead(file, contentHash ""))
        | "sed" :: rest -> sedReadFile rest
        | _ -> None

    let ofExecCommand (command: string) : Observation option =
        if System.String.IsNullOrWhiteSpace command then
            None
        elif command.Contains "$(" || command.Contains "\`" then
            None
        else
            tokenize command |> dispatchExecTokens

    // ---- Substantive Access & Dual Baselines (KR-003, KR-004, KR-010, KR-014) ----

    let isSubstantiveTool (toolName: string) : bool =
        match toolName with
        | "read"
        | "write"
        | "edit"
        | "mv"
        | "rm"
        | "create"
        | "rewrite" -> true
        | _ -> false

    let createAccessTracker () : AccessTracker = AccessTracker()

    let private withPathArg (args: obj) (record: string -> unit) : unit =
        match pathArg args with
        | Some p -> record p
        | None -> ()

    let private moveEndpoints (args: obj) : (string * string) option =
        match args?source |> text, args?destination |> text with
        | Some s, Some d -> Some(s, d)
        | _ -> None

    let private recordMoveIfCommitted (tracker: AccessTracker) (args: obj) (committed: bool) : unit =
        if committed then
            moveEndpoints args |> Option.iter (fun (s, d) -> tracker.RecordMove(s, d))

    let recordSubstantiveAccess (tracker: AccessTracker) (toolName: string) (args: obj) (committed: bool) : unit =
        match toolName with
        | "read" -> withPathArg args (fun p -> tracker.RecordRead(p, ""))
        | "write"
        | "create"
        | "edit"
        | "rewrite"
        | "rm"
        | "delete" -> withPathArg args (fun p -> tracker.RecordAttemptedMutation(p, committed))
        | "mv"
        | "move" -> recordMoveIfCommitted tracker args committed
        | _ -> ()

    let private addPath (paths: HashSet<string>) (p: string) : unit =
        if not (String.IsNullOrWhiteSpace p) then
            paths.Add p |> ignore

    let mergeFissionSubstantiveAccess (preFission: string list) (laneAccesses: string list list) : string list =
        let allPaths = HashSet<string>()

        for p in Seq.append preFission (laneAccesses |> Seq.collect id) do
            addPath allPaths p

        allPaths |> Seq.toList |> List.sort

    let caseIdentityForInvocation (sessionId: string) (invocationId: string) : string =
        sprintf "%s:%s" sessionId invocationId

    let truncateDiffForBudget (diff: string) (budget: int) : obj =
        if diff.Length <= budget then
            box
                {| text = diff
                   isTruncated = false
                   notice = "" |}
        else
            let notice = "\n[... diff truncated / 差异已截断 ...]\n"
            let available = max 0 (budget - notice.Length)
            let headLen = available / 2
            let tailLen = available - headLen
            let headText = diff.Substring(0, min headLen diff.Length)
            let tailStart = max 0 (diff.Length - tailLen)
            let tailText = diff.Substring(tailStart)
            let truncatedText = headText + notice + tailText

            box
                {| text = truncatedText
                   isTruncated = true
                   notice = "diff truncated to budget" |}

    [<Import("readFileSync", "node:fs")>]
    let private readFileBytes (path: string) : byte[] = jsNative

    [<Emit("new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode($0)")>]
    let private decodeFileUtf8 (bytes: byte[]) : string = jsNative

    let private capturedFileContent path (bytes: byte[]) (text: string) : Result<CapturedFile, string> =
        if System.Text.Encoding.UTF8.GetBytes text <> bytes then
            Error(sprintf "file %s did not preserve its UTF-8 bytes" path)
        else
            Ok
                { Bytes = bytes
                  Text = text
                  Sha256 = contentHash text }

    let private decodeFileContent path (bytes: byte[]) : Result<CapturedFile, string> =
        try
            decodeFileUtf8 bytes |> capturedFileContent path bytes
        with _ ->
            Error(sprintf "failed to read file %s: invalid UTF-8" path)

    let private classifyFileReadFailure path (error: exn) =
        if string (error?code) = "ENOENT" then
            Ok None
        else
            Error(sprintf "failed to read file %s: %s" path error.Message)

    let private readFileState (workspaceRoot: string) (relPath: string) : Result<CapturedFile option, string> =
        let fullPath = JsMutationFs.resolveToolPath workspaceRoot relPath

        try
            readFileBytes fullPath |> decodeFileContent relPath |> Result.map Some
        with ex ->
            classifyFileReadFailure relPath ex

    let private writePayloadEntry
        (store: IEventStore)
        (relPath: string)
        (content: CapturedFile)
        : Task<Result<string * JsonValue, string>> =
        task {
            match! store.WritePayload content.Bytes with
            | Error err -> return Error(sprintf "failed to write payload for %s: %s" relPath err)
            | Ok payloadRef ->
                let entryObj =
                    Encode.object
                        [ "kind", Encode.string "Present"
                          "payloadRef", Encode.string (PayloadRef.value payloadRef)
                          "payload_ref", Encode.string (PayloadRef.value payloadRef)
                          "sha256", Encode.string content.Sha256
                          "contentHash", Encode.string content.Sha256 ]

                return Ok(relPath, entryObj)
        }

    let private writeFileState (store: IEventStore) (relPath: string) (content: CapturedFile option) =
        match content with
        | None -> Task.FromResult(Ok(relPath, Encode.object [ "kind", Encode.string "Missing" ]))
        | Some captured -> writePayloadEntry store relPath captured

    let private freezeSinglePath
        (store: IEventStore)
        (workspaceRoot: string)
        (relPath: string)
        : Task<Result<string * JsonValue, string>> =
        match readFileState workspaceRoot relPath with
        | Error err -> Task.FromResult(Error err)
        | Ok content -> writeFileState store relPath content

    let rec private continueFreezeLoop store workspaceRoot rest (entries: ResizeArray<string * JsonValue>) =
        function
        | Error err -> Task.FromResult(Error err)
        | Ok entry ->
            entries.Add entry
            freezePathsLoop store workspaceRoot rest entries

    and private freezePathsLoop
        (store: IEventStore)
        (workspaceRoot: string)
        (paths: string list)
        (entries: ResizeArray<string * JsonValue>)
        : Task<Result<string, string>> =
        match paths with
        | [] ->
            let canonicalJson =
                entries |> Seq.sortBy fst |> Seq.toList |> Encode.object |> Encode.toString 0

            Task.FromResult(Ok canonicalJson)
        | relPath :: rest ->
            task {
                let! step = freezeSinglePath store workspaceRoot relPath
                return! continueFreezeLoop store workspaceRoot rest entries step
            }

    let freezeCompletionState
        (store: IEventStore)
        (workspaceRoot: string)
        (paths: string list)
        : Task<Result<string, string>> =
        let distinctPaths = paths |> List.distinct |> List.sort
        let entries = ResizeArray<string * JsonValue>()
        freezePathsLoop store workspaceRoot distinctPaths entries

    let private storedEntryDecoder: Decoder<StoredFileState> =
        Decode.field "kind" Decode.string
        |> Decode.andThen (function
            | "Missing" -> Decode.succeed StoredFileState.Missing
            | "Present" ->
                Decode.map2
                    (fun hash payload -> StoredFileState.Present(hash, PayloadRef.create payload))
                    (Decode.field "sha256" Decode.string)
                    (Decode.field "payloadRef" Decode.string)
            | kind -> Decode.fail (sprintf "unknown file state %s" kind))

    let private decodeBaseline paths baseline =
        match Decode.fromString (Decode.keyValuePairs storedEntryDecoder) baseline with
        | Error err -> Error("invalid maintenance baseline: " + err)
        | Ok entries when Set.ofList (List.map fst entries) <> Set.ofList paths ->
            Error "maintenance baseline does not cover the related paths"
        | Ok entries -> Ok(List.sortBy fst entries)

    let private requireStoredPayload path =
        function
        | None -> Error(sprintf "missing baseline payload for %s" path)
        | Some bytes -> Ok bytes

    let private validateStoredHash path hash (content: CapturedFile) =
        if content.Sha256 = hash then
            Ok()
        else
            Error(sprintf "baseline payload digest mismatch for %s" path)

    let private readPresentContent (store: IEventStore) path hash payload =
        taskResult {
            let! stored = store.ReadPayload payload
            let! bytes = requireStoredPayload path stored |> Task.FromResult
            let! content = decodeFileContent path bytes |> Task.FromResult
            do! validateStoredHash path hash content |> Task.FromResult
            return Some content.Text
        }

    let private readStoredContent store path =
        function
        | StoredFileState.Missing -> Task.FromResult(Ok None)
        | StoredFileState.Present(hash, payload) -> readPresentContent store path hash payload

    let private diffLines (text: string) =
        let lines = text.Split '\n'

        match text with
        | "" -> [||]
        | text when text.EndsWith "\n" -> lines.[.. lines.Length - 2] |> Array.map (fun line -> line + "\n")
        | _ ->
            lines
            |> Array.mapi (fun index line -> if index = lines.Length - 1 then line else line + "\n")

    let private renderDiffLine prefix (line: string) =
        if line.EndsWith "\n" then
            prefix + line
        else
            prefix + line + "\n\\ No newline at end of file\n"

    let private fileChangeKind before after =
        match before, after with
        | None, _ -> "new file\n"
        | _, None -> "deleted file\n"
        | _ -> ""

    type private LineRangePair =
        { OldStart: int
          OldEnd: int
          NewStart: int
          NewEnd: int }

    let private followEqualLines (oldLines: string[]) (newLines: string[]) range backwards diagonal start =
        let oldLength = range.OldEnd - range.OldStart
        let newLength = range.NewEnd - range.NewStart

        let oldLine index =
            oldLines.[if backwards then
                          range.OldEnd - index - 1
                      else
                          range.OldStart + index]

        let newLine index =
            newLines.[if backwards then
                          range.NewEnd - index - 1
                      else
                          range.NewStart + index]
        // DSL-MUTABLE: algorithm-scratch — end of the equal-line run on this diagonal.
        let mutable position = start

        while position >= 0
              && position < oldLength
              && position - diagonal < newLength
              && oldLine position = newLine (position - diagonal) do
            position <- position + 1

        position

    let private tryDiffOverlap oldLength newLength backwards diagonal oldPosition (opposite: int[]) oppositeDepth =
        let otherDiagonal = oldLength - newLength - diagonal
        let offset = opposite.Length / 2

        let otherPosition =
            match oppositeDepth with
            | Some otherDepth when oldPosition >= 0 && abs otherDiagonal <= otherDepth ->
                opposite.[offset + otherDiagonal]
            | _ -> -1

        match otherPosition with
        | position when position < 0 || oldPosition + position < oldLength -> None
        | position when backwards -> Some(position, position - otherDiagonal)
        | _ -> Some(oldPosition, oldPosition - diagonal)

    let private nextDiffPosition oldLength newLength depth diagonal (frontier: int[]) =
        let offset = frontier.Length / 2

        let insertion =
            if diagonal < depth then
                frontier.[offset + diagonal + 1]
            else
                -1

        let deletion =
            if diagonal > -depth then
                frontier.[offset + diagonal - 1]
            else
                -1

        let afterInsertion =
            if insertion >= 0 && insertion - diagonal - 1 < newLength then
                insertion
            else
                -1

        let afterDeletion =
            if deletion >= 0 && deletion < oldLength then
                deletion + 1
            else
                -1

        if depth = 0 then 0 else max afterInsertion afterDeletion

    let private advanceDiffFrontier
        (oldLines: string[])
        (newLines: string[])
        range
        backwards
        depth
        (frontier: int[])
        (opposite: int[])
        oppositeDepth
        =
        let oldLength = range.OldEnd - range.OldStart
        let newLength = range.NewEnd - range.NewStart
        let offset = frontier.Length / 2
        // DSL-MUTABLE: algorithm-scratch — current Myers wavefront diagonal.
        let mutable diagonal = -depth
        // DSL-MUTABLE: algorithm-scratch — first overlapping forward/reverse path.
        let mutable split = None

        while diagonal <= depth && split.IsNone do
            let start = nextDiffPosition oldLength newLength depth diagonal frontier
            let oldPosition = followEqualLines oldLines newLines range backwards diagonal start
            frontier.[offset + diagonal] <- oldPosition
            split <- tryDiffOverlap oldLength newLength backwards diagonal oldPosition opposite oppositeDepth
            diagonal <- diagonal + 2

        split

    let private middleDiffSplit oldLines newLines range =
        let oldLength = range.OldEnd - range.OldStart
        let newLength = range.NewEnd - range.NewStart
        let maxDepth = (oldLength + newLength + 1) / 2
        let forward = Array.create (2 * maxDepth + 3) -1
        let backward = Array.create forward.Length -1
        let oddDistance = (oldLength - newLength) % 2 <> 0
        // DSL-MUTABLE: algorithm-scratch — bidirectional edit distance.
        let mutable depth = 0
        // DSL-MUTABLE: algorithm-scratch — first meeting point at the shortest distance.
        let mutable split = None

        while split.IsNone && depth <= maxDepth do
            split <-
                advanceDiffFrontier
                    oldLines
                    newLines
                    range
                    false
                    depth
                    forward
                    backward
                    (if oddDistance then Some(depth - 1) else None)
                |> Option.orElseWith (fun () ->
                    advanceDiffFrontier
                        oldLines
                        newLines
                        range
                        true
                        depth
                        backward
                        forward
                        (if oddDistance then None else Some depth))

            depth <- depth + 1

        match split with
        | Some(oldPosition, newPosition) when
            (oldPosition > 0 || newPosition > 0)
            && (oldPosition < oldLength || newPosition < newLength)
            ->
            range.OldStart + oldPosition, range.NewStart + newPosition
        | _ -> invalidOp "line diff bisection did not make progress"

    let private trimEqualLines (oldLines: string[]) (newLines: string[]) range =
        let commonLength =
            min (range.OldEnd - range.OldStart) (range.NewEnd - range.NewStart)

        let prefix =
            Seq.init commonLength id
            |> Seq.takeWhile (fun index -> oldLines.[range.OldStart + index] = newLines.[range.NewStart + index])
            |> Seq.length

        let suffix =
            Seq.init (commonLength - prefix) id
            |> Seq.takeWhile (fun index -> oldLines.[range.OldEnd - index - 1] = newLines.[range.NewEnd - index - 1])
            |> Seq.length

        { OldStart = range.OldStart + prefix
          OldEnd = range.OldEnd - suffix
          NewStart = range.NewStart + prefix
          NewEnd = range.NewEnd - suffix }

    let private haveCommonLine (oldLines: string[]) (newLines: string[]) range =
        let oldSet =
            HashSet<string>(seq { for index in range.OldStart .. range.OldEnd - 1 -> oldLines.[index] })

        seq { for index in range.NewStart .. range.NewEnd - 1 -> newLines.[index] }
        |> Seq.exists oldSet.Contains

    let private appendChangedRange (changes: ResizeArray<LineRangePair>) range =
        if
            changes.Count > 0
            && changes.[changes.Count - 1].OldEnd = range.OldStart
            && changes.[changes.Count - 1].NewEnd = range.NewStart
        then
            changes.[changes.Count - 1] <-
                { changes.[changes.Count - 1] with
                    OldEnd = range.OldEnd
                    NewEnd = range.NewEnd }
        else
            changes.Add range

    let private enqueueDiffHalves oldLines newLines (pending: ResizeArray<LineRangePair>) range =
        let oldSplit, newSplit = middleDiffSplit oldLines newLines range

        pending.Add
            { range with
                OldStart = oldSplit
                NewStart = newSplit }

        pending.Add
            { range with
                OldEnd = oldSplit
                NewEnd = newSplit }

    let private processDiffRange oldLines newLines pending changes range =
        match range with
        | range when range.OldStart = range.OldEnd && range.NewStart = range.NewEnd -> ()
        | range when
            range.OldStart = range.OldEnd
            || range.NewStart = range.NewEnd
            || not (haveCommonLine oldLines newLines range)
            ->
            appendChangedRange changes range
        | range -> enqueueDiffHalves oldLines newLines pending range

    let private changedLineRanges (oldLines: string[]) (newLines: string[]) =
        let pending = ResizeArray<LineRangePair>()
        let changes = ResizeArray<LineRangePair>()

        pending.Add
            { OldStart = 0
              OldEnd = oldLines.Length
              NewStart = 0
              NewEnd = newLines.Length }

        while pending.Count > 0 do
            let range = pending.[pending.Count - 1] |> trimEqualLines oldLines newLines
            pending.RemoveAt(pending.Count - 1)
            processDiffRange oldLines newLines pending changes range

        changes |> Seq.toList

    let private renderDiffHunk (oldLines: string[]) (newLines: string[]) range =
        let removed = range.OldEnd - range.OldStart
        let added = range.NewEnd - range.NewStart

        let start position count =
            if count = 0 then position else position + 1

        sprintf "@@ -%d,%d +%d,%d @@\n" (start range.OldStart removed) removed (start range.NewStart added) added
        + (oldLines.[range.OldStart .. range.OldEnd - 1]
           |> Array.map (renderDiffLine "-")
           |> String.concat "")
        + (newLines.[range.NewStart .. range.NewEnd - 1]
           |> Array.map (renderDiffLine "+")
           |> String.concat "")

    let private fileDiff path (before: string option) (after: string option) =
        if before = after then
            ""
        else
            let oldLines = before |> Option.defaultValue "" |> diffLines
            let newLines = after |> Option.defaultValue "" |> diffLines
            let oldPath = if before.IsSome then "a/" + path else "/dev/null"
            let newPath = if after.IsSome then "b/" + path else "/dev/null"

            sprintf "diff --git a/%s b/%s\n%s--- %s\n+++ %s\n" path path (fileChangeKind before after) oldPath newPath
            + (changedLineRanges oldLines newLines
               |> List.map (renderDiffHunk oldLines newLines)
               |> String.concat "")

    let rec private captureMaintenancePaths store workspaceRoot entries captured diffs =
        taskResult {
            match entries with
            | [] ->
                return
                    { DiffSummary = diffs |> List.rev |> String.concat ""
                      TargetState = captured |> List.rev |> Encode.object |> Encode.toString 0 }
            | (path, state) :: rest ->
                let! before = readStoredContent store path state
                let! target = readFileState workspaceRoot path |> Task.FromResult
                let! stored = writeFileState store path target
                let diff = fileDiff path before (target |> Option.map (fun content -> content.Text))
                return! captureMaintenancePaths store workspaceRoot rest (stored :: captured) (diff :: diffs)
        }

    let computeMaintenanceDiff
        (store: IEventStore)
        (workspaceRoot: string)
        (paths: string list)
        (baseline: string)
        : Task<Result<MaintenanceCapture, string>> =
        taskResult {
            let! entries = decodeBaseline paths baseline |> Task.FromResult
            return! captureMaintenancePaths store workspaceRoot entries [] []
        }
