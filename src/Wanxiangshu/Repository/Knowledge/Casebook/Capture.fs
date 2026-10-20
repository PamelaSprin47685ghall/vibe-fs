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
          Refresh: bool
          TargetState: string }

    [<Import("CasebookMaintenanceLaw", "wanxiangli/casebook-maintenance")>]
    let private maintenanceLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    let private maintenanceLaw: obj = construct maintenanceLawClass

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
        | "create"
        | "rewrite"
        | "delete" -> withPathArg args (fun p -> tracker.RecordAttemptedMutation(p, committed))
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

    /// knowledge-reuse-014: head/tail truncation is wanxiangli/casebook-maintenance `truncate`.
    let truncateDiffForBudget (diff: string) (budget: int) : obj = maintenanceLaw?truncate (diff, budget)

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

    /// knowledge-reuse-016: only ENOENT is Missing (wanxiangli/casebook-maintenance `readFailure`).
    let private classifyFileReadFailure path (error: exn) =
        if unbox<string> (maintenanceLaw?readFailure (error?code)) = "missing" then
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

    /// knowledge-reuse-005/015/016: the B→T file diff is wanxiangli/casebook-maintenance
    /// `CasebookMaintenanceLaw.fileDiff` (CUT-27); null is Missing.
    let private fileDiff (path: string) (before: string option) (after: string option) : string =
        unbox<string> (maintenanceLaw?fileDiff (path, Option.toObj before, Option.toObj after))

    let rec private captureMaintenancePaths store workspaceRoot entries captured diffs =
        taskResult {
            match entries with
            | [] ->
                let summary = maintenanceLaw?summarize (diffs |> List.rev |> List.toArray)

                return
                    { DiffSummary = unbox<string> summary?diffSummary
                      Refresh = unbox<bool> summary?refresh
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
