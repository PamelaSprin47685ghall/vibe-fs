module Wanxiangshu.Shell.FuzzyToolsCodec

open Fable.Core
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.FuzzySearch
open Wanxiangshu.Shell.FuzzySearchHelpers

let private patternRequiredOnFirstCall = "pattern is required on the first call"

let private hasResumeIterator (iterator: string option) : bool =
    match iterator with
    | Some it -> it.Trim() <> ""
    | None -> false

let private requirePositiveOptInt (tool: string) (field: string) (value: int option) : Result<unit, DomainError> =
    match value with
    | Some n when n < 1 -> Error(InvalidIntent(tool, field, "must be >= 1"))
    | _ -> Ok()

let private validateFuzzyFirstCall
    (tool: string)
    (pattern: string option)
    (iterator: string option)
    (limit: int option)
    (context: int option)
    : Result<unit, DomainError> =
    let patternOk =
        if hasResumeIterator iterator then
            Ok()
        else
            match pattern with
            | Some p when p.Trim() <> "" -> Ok()
            | _ -> Error(InvalidIntent(tool, "pattern", patternRequiredOnFirstCall))

    patternOk
    |> Result.bind (fun () -> requirePositiveOptInt tool "limit" limit)
    |> Result.bind (fun () ->
        match context with
        | Some _ -> requirePositiveOptInt tool "context" context
        | None -> Ok())

let private patternsField (tool: string) (args: obj) : Result<string list, DomainError> =
    let v = Dyn.get args "pattern"
    Ok(parseJsonArrayOrString v)

let private patternHead (patterns: string list) : string option =
    match patterns with
    | [] -> None
    | head :: _ -> Some head

let decodeFuzzyFindArgs (args: obj) : Result<FuzzyFindParams, DomainError> =
    patternsField "fuzzy_find" args
    |> Result.bind (fun patterns ->
        let iterator = strField args "iterator"
        let limit = optInt args "limit"

        validateFuzzyFirstCall "fuzzy_find" (patternHead patterns) iterator limit None
        |> Result.map (fun () ->
            { pattern = patterns
              path = strField args "path"
              limit = limit
              iterator = iterator }))

let decodeFuzzyGrepArgs (args: obj) : Result<FuzzyGrepParams, DomainError> =
    patternsField "fuzzy_grep" args
    |> Result.bind (fun patterns ->
        let iterator = strField args "iterator"
        let limit = optInt args "limit"
        let context = optInt args "context"

        validateFuzzyFirstCall "fuzzy_grep" (patternHead patterns) iterator limit context
        |> Result.map (fun () ->
            { pattern = patterns
              path = strField args "path"
              exclude = parseExcludeField args
              searchIgnored = optBool args "searchIgnored"
              caseSensitive = optBool args "caseSensitive"
              context = context
              limit = limit
              iterator = iterator }))
