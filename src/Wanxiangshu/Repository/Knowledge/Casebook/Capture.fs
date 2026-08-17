namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// CASE-003: typed observation capture from the final execution layer.
///
/// Captures happen at the Host tool-execution boundary (tool.execute.after:
/// args + rendered output) — never from transcript text. Capture is
/// best-effort: an unparseable execution yields None, which only means one
/// fewer change-detection opportunity, never a failed Inspector call.
module CasebookCapture =

    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    [<Emit("$0.update($1, 'utf8').digest('hex')")>]
    let private digestHex (hash: obj) (data: string) : string = jsNative

    /// Stable content fingerprint for FileRead observations (CASE-003).
    let contentHash (text: string) : string =
        if isNull text then
            ""
        else
            digestHex (createHash "sha256") text

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
            | '\'' :: rest -> go rest current (not inQuote) acc
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
        // sed -n 'SCRIPT' file — the script is the first non-option
        // token (quotes already stripped by tokenize), the file is the
        // one after it. A bare `sed file` (no script) is skipped.
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
            // Skip options and their values (-n 30, -100, -f); the first
            // remaining token is the file. `cat file | grep bar` lands here
            // too and counts as reading file.
            firstReadFile rest
            |> Option.map (fun file -> Observation.FileRead(file, contentHash ""))
        | "sed" :: rest -> sedReadFile rest
        | _ -> None

    let ofExecCommand (command: string) : Observation option =
        if System.String.IsNullOrWhiteSpace command then
            None
        elif command.Contains "$(" || command.Contains "`" then
            None
        else
            tokenize command |> dispatchExecTokens
