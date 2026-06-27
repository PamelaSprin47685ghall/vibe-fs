module Wanxiangshu.Shell.ExecutorToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField

type ExecutorArgs = {
    Language: ExecutorLanguage
    Program: string
    Dependencies: string list
    TimeoutType: ExecutorTimeoutType
    Mode: string
    Warn: ExecutorWarn
}

let private strListField (a: obj) (k: string) : string list =
    let v = Dyn.get a k
    if Dyn.isNullish v then []
    elif Dyn.isArray v then (v :?> obj array) |> Array.map string |> Array.toList
    else [ string v ]

let private parseLanguageField (value: string) : Result<ExecutorLanguage, DomainError> =
    match value.Trim().ToLowerInvariant() with
    | "shell" -> Ok Shell
    | "python" -> Ok Python
    | "javascript" -> Ok Javascript
    | _ -> Error (InvalidIntent ("executor", "language", "expected shell, python, or javascript"))

let private parseModeField (value: string) : Result<string, DomainError> =
    match value.Trim() with
    | "ro" | "rw" as m -> Ok m
    | _ -> Error (InvalidIntent ("executor", "mode", "must be ro or rw"))

let private parseWarnField (value: string) : Result<ExecutorWarn, DomainError> =
    match value.Trim() with
    | "it-is-not-possible-to-do-it-using-other-tools" -> Ok ItIsNotPossibleToDoThisUsingOtherTools
    | _ -> Error (InvalidIntent ("executor", "warn", "must be 'it-is-not-possible-to-do-it-using-other-tools'"))

let peekExecutorMode (args: obj) : string option =
    strField args "mode" |> Option.map (fun s -> s.Trim())

let decodeExecutorArgs (args: obj) : Result<ExecutorArgs, DomainError> =
    match strField args "language" with
    | None -> Error (InvalidIntent ("executor", "language", "required"))
    | Some langStr ->
        match parseLanguageField langStr with
        | Error e -> Error e
        | Ok language ->
            match strField args "program" with
            | None -> Error (InvalidIntent ("executor", "program", "required"))
            | Some program when System.String.IsNullOrWhiteSpace program ->
                Error (InvalidIntent ("executor", "program", "required"))
            | Some program ->
                match strField args "mode" with
                | None -> Error (InvalidIntent ("executor", "mode", "required"))
                | Some modeStr ->
                    match parseModeField modeStr with
                    | Error e -> Error e
                    | Ok mode ->
                        match strField args "warn" with
                        | None -> Error (InvalidIntent ("executor", "warn", "required"))
                        | Some warnStr ->
                            match parseWarnField warnStr with
                            | Error e -> Error e
                            | Ok warn ->
                                let timeoutRaw = defaultArg (strField args "timeout_type") ""
                                Ok {
                                    Language = language
                                    Program = program
                                    Dependencies = strListField args "dependencies"
                                    TimeoutType = parseTimeout timeoutRaw
                                    Mode = mode
                                    Warn = warn
                                }

let toExecuteOptions (cwd: string option) (decoded: ExecutorArgs) : ExecuteOptions =
    { program = decoded.Program
      language = decoded.Language
      dependencies = decoded.Dependencies
      timeoutType = decoded.TimeoutType
      mode = decoded.Mode
      cwd = cwd
      whatToSummarize = "" }