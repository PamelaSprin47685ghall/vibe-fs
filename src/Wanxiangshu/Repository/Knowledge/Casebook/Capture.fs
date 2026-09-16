namespace Wanxiangshu.Repository.Knowledge.Casebook

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Repository.Programming.Js

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

    let private observationPathAndHash (obs: Observation) : string * string =
        match obs with
        | Observation.FileRead(p, h) -> p, h
        | Observation.GlobResult(p, _)
        | Observation.GrepResult(p, _) -> p, ""

    let private setPresentIfAbsent (m: obj) (p: string) (h: string) : unit =
        if not (String.IsNullOrWhiteSpace p) && not (emitJsExpr (m, p) "$0.has($1)") then
            emitJsExpr (m, p, h) "$0.set($1, { kind: 'Present', contentHash: $2, content: '' })"
            |> ignore

    /// Baseline map for diff maintenance: observed files first, then every
    /// related path the case associates, each as a whole-file Present entry.
    let baselineFromObservations (observations: Observation list) (relatedPaths: string list) : obj =
        let m = emitJsExpr () "new Map()"

        for obs in observations do
            let p, h = observationPathAndHash obs
            setPresentIfAbsent m p h

        for p in relatedPaths do
            setPresentIfAbsent m p ""

        m

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

    [<Emit("new Map()")>]
    let private newJsMap () : obj = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private jsMapSet (map: obj) (key: obj) (value: obj) : unit = jsNative

    [<Emit("$0.get($1)")>]
    let private jsMapGet (map: obj) (key: obj) : obj = jsNative

    [<Emit("$0.keys()")>]
    let private jsMapKeys (map: obj) : obj = jsNative

    [<Emit("Array.from($0)")>]
    let private jsArrayFrom (iterable: obj) : obj array = jsNative

    [<Emit("$0 && typeof $0.get === 'function'")>]
    let private isJsMap (value: obj) : bool = jsNative

    let private completionEntry (fullPath: string) : obj =
        match JsUtf8Fs.readUtf8Classified fullPath with
        | Ok text ->
            box
                {| kind = "Present"
                   contentHash = contentHash text
                   content = text |}
        | Error _ -> box {| kind = "Missing" |}

    let private completionEntryAt (workspaceRoot: string) (relPath: string) : obj =
        let fullPath = JsMutationFs.resolveToolPath workspaceRoot relPath

        if JsMutationFs.existsPath fullPath then
            completionEntry fullPath
        else
            box {| kind = "Missing" |}

    let freezeCompletionState (workspaceRoot: string) (paths: string list) : Task<obj> =
        task {
            let resultMap = newJsMap ()

            for relPath in paths do
                jsMapSet resultMap (box relPath) (completionEntryAt workspaceRoot relPath)

            return resultMap
        }

    let private kindOfEntry (entry: obj) : string =
        if isNull entry || isNull (entry?kind) then
            "Missing"
        else
            string (entry?kind)

    let private hashOfEntry (entry: obj) : string =
        if isNull entry || isNull (entry?contentHash) then
            ""
        else
            string (entry?contentHash)

    let private contentOfEntry (entry: obj) : string =
        if isNull entry || isNull (entry?content) then
            ""
        else
            string (entry?content)

    let private addedFileDiff (relPath: string) (curText: string) : string =
        sprintf "diff --git a/%s b/%s\nnew file\n--- /dev/null\n+++ b/%s\n+%s" relPath relPath relPath curText

    let private changedFileDiff (relPath: string) (baseContent: string) (curText: string) : string =
        sprintf "diff --git a/%s b/%s\n--- a/%s\n+++ b/%s\n-%s\n+%s" relPath relPath relPath relPath baseContent curText

    let private deletedFileDiff (relPath: string) (baseContent: string) : string =
        sprintf "diff --git a/%s b/%s\ndeleted file\n--- a/%s\n+++ /dev/null\n-%s" relPath relPath relPath baseContent

    let computeMaintenanceDiff (workspaceRoot: string) (baseline: obj) : Task<obj> =
        task {
            let mutable hasDiff = false
            let diffLines = ResizeArray<string>()

            let keys =
                if isJsMap baseline then
                    jsArrayFrom (jsMapKeys baseline) |> Array.map string |> Array.toList
                else
                    []

            let record (line: string) =
                hasDiff <- true
                diffLines.Add line

            let diffForPath (relPath: string) : string option =
                let baseEntry = jsMapGet baseline (box relPath)
                let baseKind = kindOfEntry baseEntry
                let baseHash = hashOfEntry baseEntry
                let baseContent = contentOfEntry baseEntry
                let fullPath = JsMutationFs.resolveToolPath workspaceRoot relPath

                let current =
                    if JsMutationFs.existsPath fullPath then
                        JsUtf8Fs.readUtf8Classified fullPath |> Result.toOption
                    else
                        None

                match current, baseKind with
                | Some curText, "Missing" -> Some(addedFileDiff relPath curText)
                | Some curText, _ when baseHash <> contentHash curText ->
                    Some(changedFileDiff relPath baseContent curText)
                | Some _, _ -> None
                | None, "Present" -> Some(deletedFileDiff relPath baseContent)
                | None, _ -> None

            for relPath in keys do
                diffForPath relPath |> Option.iter record

            let diffSummary = String.concat "\n" diffLines

            return
                box
                    {| hasDiff = hasDiff
                       diffSummary = diffSummary |}
        }
