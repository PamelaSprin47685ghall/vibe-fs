module Wanxiangshu.Shell.FuzzyToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.FuzzySearch

let private patternRequiredOnFirstCall = "pattern is required on the first call"

let private hasResumeIterator (iterator: string option) : bool =
    match iterator with
    | Some it -> it.Trim() <> ""
    | None -> false

let private requirePositiveOptInt (tool: string) (field: string) (value: int option) : Result<unit, DomainError> =
    match value with
    | Some n when n < 1 -> Error (InvalidIntent (tool, field, "must be >= 1"))
    | _ -> Ok ()

let private validateFuzzyFirstCall (tool: string) (pattern: string option) (iterator: string option) (limit: int option) (context: int option)
    : Result<unit, DomainError> =
    let patternOk =
        if hasResumeIterator iterator then Ok ()
        else
            match pattern with
            | Some p when p.Trim() <> "" -> Ok ()
            | _ -> Error (InvalidIntent (tool, "pattern", patternRequiredOnFirstCall))
    patternOk
    |> Result.bind (fun () -> requirePositiveOptInt tool "limit" limit)
    |> Result.bind (fun () ->
        match context with
        | Some _ -> requirePositiveOptInt tool "context" context
        | None -> Ok ())

let decodeFuzzyFindArgs (args: obj) : Result<FuzzyFindParams, DomainError> =
    let pattern = strField args "pattern"
    let iterator = strField args "iterator"
    let limit = optInt args "limit"
    validateFuzzyFirstCall "fuzzy_find" pattern iterator limit None
    |> Result.map (fun () ->
        { pattern = pattern
          path = strField args "path"
          limit = limit
          iterator = iterator })

let decodeFuzzyGrepArgs (args: obj) : Result<FuzzyGrepParams, DomainError> =
    let pattern = strField args "pattern"
    let iterator = strField args "iterator"
    let limit = optInt args "limit"
    let context = optInt args "context"
    validateFuzzyFirstCall "fuzzy_grep" pattern iterator limit context
    |> Result.map (fun () ->
        { pattern = pattern
          path = strField args "path"
          exclude = parseExcludeField args
          caseSensitive = optBool args "caseSensitive"
          context = context
          limit = limit
          iterator = iterator })