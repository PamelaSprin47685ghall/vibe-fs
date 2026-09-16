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

    member _.RecordRead(path: string, _contentHash: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then paths.Add path |> ignore

    member _.RecordCreate(path: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then paths.Add path |> ignore

    member _.RecordEdit(path: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then paths.Add path |> ignore

    member _.RecordDelete(path: string) : unit =
        if not (String.IsNullOrWhiteSpace path) then paths.Add path |> ignore

    member _.RecordMove(source: string, destination: string) : unit =
        if not (String.IsNullOrWhiteSpace source) then paths.Add source |> ignore
        if not (String.IsNullOrWhiteSpace destination) then paths.Add destination |> ignore

    member _.RecordGrep(_pattern: string, _path: string) : unit = ()
    member _.RecordGlob(_pattern: string) : unit = ()

    member _.RecordAttemptedMutation(path: string, committed: bool) : unit =
        if committed && not (String.IsNullOrWhiteSpace path) then
            paths.Add path |> ignore

    member _.GetRelatedPaths() : string list =
        paths |> Seq.toList |> List.sort

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
        if System.String.IsNullOrWhiteSpace command then None
        elif command.Contains "$(" || command.Contains "\`" then None
        else tokenize command |> dispatchExecTokens

    // ---- Substantive Access & Dual Baselines (KR-003, KR-004, KR-010, KR-014) ----

    let isSubstantiveTool (toolName: string) : bool =
        match toolName with
        | "read" | "write" | "edit" | "mv" | "rm" | "create" | "rewrite" -> true
        | _ -> false

    let createAccessTracker () : AccessTracker = AccessTracker()

    let recordSubstantiveAccess (tracker: AccessTracker) (toolName: string) (args: obj) (committed: bool) : unit =
        match toolName with
        | "read" ->
            match pathArg args with
            | Some p -> tracker.RecordRead(p, "")
            | None -> ()
        | "write" | "create" ->
            match pathArg args with
            | Some p -> tracker.RecordAttemptedMutation(p, committed)
            | None -> ()
        | "edit" | "rewrite" ->
            match pathArg args with
            | Some p -> tracker.RecordAttemptedMutation(p, committed)
            | None -> ()
        | "rm" | "delete" ->
            match pathArg args with
            | Some p -> tracker.RecordAttemptedMutation(p, committed)
            | None -> ()
        | "mv" | "move" ->
            if committed then
                let src = args?source |> text
                let dst = args?destination |> text
                match src, dst with
                | Some s, Some d -> tracker.RecordMove(s, d)
                | _ -> ()
        | _ -> ()

    let mergeFissionSubstantiveAccess (preFission: string list) (laneAccesses: string list list) : string list =
        let allPaths = HashSet<string>()
        for p in preFission do
            if not (String.IsNullOrWhiteSpace p) then allPaths.Add p |> ignore
        for lane in laneAccesses do
            for p in lane do
                if not (String.IsNullOrWhiteSpace p) then allPaths.Add p |> ignore
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

    let freezeCompletionState (workspaceRoot: string) (paths: string list) : Task<obj> =
        task {
            let resultMap = newJsMap ()
            for relPath in paths do
                let fullPath = JsMutationFs.resolveToolPath workspaceRoot relPath
                if JsMutationFs.existsPath fullPath then
                    match JsUtf8Fs.readUtf8Classified fullPath with
                    | Ok text ->
                        let entry = box {| kind = "Present"; contentHash = contentHash text; content = text |}
                        jsMapSet resultMap (box relPath) entry
                    | Error _ ->
                        let entry = box {| kind = "Missing" |}
                        jsMapSet resultMap (box relPath) entry
                else
                    let entry = box {| kind = "Missing" |}
                    jsMapSet resultMap (box relPath) entry
            return resultMap
        }

    let computeMaintenanceDiff (workspaceRoot: string) (baseline: obj) : Task<obj> =
        task {
            let mutable hasDiff = false
            let diffLines = ResizeArray<string>()

            let keys =
                if isJsMap baseline then
                    jsArrayFrom (jsMapKeys baseline) |> Array.map string |> Array.toList
                else
                    []

            for relPath in keys do
                let baseEntry = jsMapGet baseline (box relPath)
                let baseKind = if isNull baseEntry then "Missing" else string (baseEntry?kind)
                let baseHash = if isNull baseEntry then "" else string (baseEntry?contentHash)
                let baseContent = if isNull baseEntry || isNull (baseEntry?content) then "" else string (baseEntry?content)

                let fullPath = JsMutationFs.resolveToolPath workspaceRoot relPath
                if JsMutationFs.existsPath fullPath then
                    match JsUtf8Fs.readUtf8Classified fullPath with
                    | Ok curText ->
                        let curHash = contentHash curText
                        if baseKind = "Missing" then
                            hasDiff <- true
                            diffLines.Add(sprintf "diff --git a/%s b/%s\nnew file\n--- /dev/null\n+++ b/%s\n+%s" relPath relPath relPath curText)
                        elif baseHash <> curHash then
                            hasDiff <- true
                            diffLines.Add(sprintf "diff --git a/%s b/%s\n--- a/%s\n+++ b/%s\n-%s\n+%s" relPath relPath relPath relPath baseContent curText)
                    | Error _ ->
                        if baseKind = "Present" then
                            hasDiff <- true
                            diffLines.Add(sprintf "diff --git a/%s b/%s\ndeleted file\n--- a/%s\n+++ /dev/null\n-%s" relPath relPath relPath baseContent)
                else
                    if baseKind = "Present" then
                        hasDiff <- true
                        diffLines.Add(sprintf "diff --git a/%s b/%s\ndeleted file\n--- a/%s\n+++ /dev/null\n-%s" relPath relPath relPath baseContent)

            let diffSummary = String.concat "\n" diffLines
            return box {| hasDiff = hasDiff; diffSummary = diffSummary |}
        }
