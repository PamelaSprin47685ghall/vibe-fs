module Wanxiangshu.Runtime.ExecutorToolsCodec

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.DynField

type ExecutorArgs =
    { Language: ExecutorLanguage
      Command: string
      Dependencies: string list
      TimeoutType: ExecutorTimeoutType
      WhatToSummarize: string
      MaxBytes: int }

let private parseLanguageField (value: string) : Result<ExecutorLanguage, DomainError> =
    match value.Trim().ToLowerInvariant() with
    | "shell" -> Ok Shell
    | "python" -> Ok Python
    | "javascript" -> Ok Javascript
    | _ -> Error(InvalidIntent("executor", "language", "expected shell, python, or javascript"))

let decodeExecutorArgs (args: obj) : Result<ExecutorArgs, DomainError> =
    let languageResult =
        match strField args "language" with
        | None -> Ok Shell
        | Some langStr -> parseLanguageField langStr

    let commandResult =
        match strField args "command" with
        | None -> Error(InvalidIntent("executor", "command", "required"))
        | Some p when System.String.IsNullOrWhiteSpace p -> Error(InvalidIntent("executor", "command", "required"))
        | Some p -> Ok p

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
        commandResult
        |> Result.bind (fun command ->
            whatResult
            |> Result.bind (fun whatToSummarize ->
                maxBytesResult
                |> Result.map (fun maxBytes ->
                    { Language = language
                      Command = command
                      Dependencies = defaultArg (strListField args "dependencies") []
                      TimeoutType = parseTimeout (defaultArg (strField args "timeout_type") "")
                      WhatToSummarize = whatToSummarize
                      MaxBytes = maxBytes }))))

let toExecuteOptions (cwd: string option) (decoded: ExecutorArgs) : ExecuteOptions =
    { command = decoded.Command
      language = decoded.Language
      dependencies = decoded.Dependencies
      timeoutType = decoded.TimeoutType
      cwd = cwd
      whatToSummarize = decoded.WhatToSummarize
      maxBytes = decoded.MaxBytes }
