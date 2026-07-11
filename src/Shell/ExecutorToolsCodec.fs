module Wanxiangshu.Shell.ExecutorToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Shell.DynField

type ExecutorArgs =
    { Language: ExecutorLanguage
      Program: string
      Dependencies: string list
      TimeoutType: ExecutorTimeoutType
      Mode: string
      WhatToSummarize: string
      MaxBytes: int }

let private parseLanguageField (value: string) : Result<ExecutorLanguage, DomainError> =
    match value.Trim().ToLowerInvariant() with
    | "shell" -> Ok Shell
    | "python" -> Ok Python
    | "javascript" -> Ok Javascript
    | _ -> Error(InvalidIntent("executor", "language", "expected shell, python, or javascript"))

let private parseModeField (value: string) : Result<string, DomainError> =
    match value.Trim() with
    | "ro"
    | "rw" as m -> Ok m
    | _ -> Error(InvalidIntent("executor", "mode", "must be ro or rw"))

let peekExecutorMode (args: obj) : string option =
    strField args "mode" |> Option.map (fun s -> s.Trim())

let decodeExecutorArgs (args: obj) : Result<ExecutorArgs, DomainError> =
    let languageResult =
        match strField args "language" with
        | None -> Ok Shell
        | Some langStr -> parseLanguageField langStr

    let programResult =
        match strField args "program" with
        | None -> Error(InvalidIntent("executor", "program", "required"))
        | Some p when System.String.IsNullOrWhiteSpace p -> Error(InvalidIntent("executor", "program", "required"))
        | Some p -> Ok p

    let modeResult =
        match strField args "mode" with
        | None -> Error(InvalidIntent("executor", "mode", "required"))
        | Some modeStr -> parseModeField modeStr

    let whatResult =
        match strField args "what_to_summarize" with
        | None -> Error(InvalidIntent("executor", "what_to_summarize", "required"))
        | Some w when System.String.IsNullOrWhiteSpace w ->
            Error(InvalidIntent("executor", "what_to_summarize", "required"))
        | Some w -> Ok w

    let maxBytesResult =
        match optInt args "max_bytes" with
        | None -> Error(InvalidIntent("executor", "max_bytes", "required"))
        | Some mb -> Ok mb

    languageResult
    |> Result.bind (fun language ->
        programResult
        |> Result.bind (fun program ->
            modeResult
            |> Result.bind (fun mode ->
                whatResult
                |> Result.bind (fun whatToSummarize ->
                    maxBytesResult
                    |> Result.map (fun maxBytes ->
                        { Language = language
                          Program = program
                          Dependencies = defaultArg (strListField args "dependencies") []
                          TimeoutType = parseTimeout (defaultArg (strField args "timeout_type") "")
                          Mode = mode
                          WhatToSummarize = whatToSummarize
                          MaxBytes = maxBytes })))))

let toExecuteOptions (cwd: string option) (decoded: ExecutorArgs) : ExecuteOptions =
    { program = decoded.Program
      language = decoded.Language
      dependencies = decoded.Dependencies
      timeoutType = decoded.TimeoutType
      mode = decoded.Mode
      cwd = cwd
      whatToSummarize = decoded.WhatToSummarize
      maxBytes = decoded.MaxBytes }
