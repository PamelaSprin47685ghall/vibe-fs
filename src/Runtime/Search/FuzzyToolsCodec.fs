module Wanxiangshu.Runtime.FuzzyToolsCodec

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.FuzzySearch
open Wanxiangshu.Runtime.FuzzySearchSupport

let private requirePositiveOptInt (tool: string) (field: string) (value: int option) : Result<unit, DomainError> =
    match value with
    | Some n when n < 1 -> Error(InvalidIntent(tool, field, "must be >= 1"))
    | _ -> Ok()

let private patternsField (tool: string) (args: obj) : Result<string list, DomainError> =
    let v = Dyn.get args "pattern"
    Ok(parseJsonArrayOrString v)

let decodeFuzzyFindArgs (args: obj) : Result<FuzzyFindParams, DomainError> =
    patternsField "fuzzy_find" args
    |> Result.bind (fun patterns ->
        match patterns with
        | [] -> Error(InvalidIntent("fuzzy_find", "pattern", "pattern is required"))
        | first :: _ when first.Trim() = "" -> Error(InvalidIntent("fuzzy_find", "pattern", "pattern cannot be empty"))
        | _ ->
            let limit = optInt args "limit"

            requirePositiveOptInt "fuzzy_find" "limit" limit
            |> Result.map (fun () ->
                { pattern = patterns
                  path = strField args "path"
                  limit = limit }))

let decodeFuzzyGrepArgs (args: obj) : Result<FuzzyGrepParams, DomainError> =
    patternsField "fuzzy_grep" args
    |> Result.bind (fun patterns ->
        match patterns with
        | [] -> Error(InvalidIntent("fuzzy_grep", "pattern", "pattern is required"))
        | first :: _ when first.Trim() = "" -> Error(InvalidIntent("fuzzy_grep", "pattern", "pattern cannot be empty"))
        | _ ->
            let limit = optInt args "limit"
            let context = optInt args "context"

            requirePositiveOptInt "fuzzy_grep" "limit" limit
            |> Result.bind (fun () ->
                match context with
                | Some _ -> requirePositiveOptInt "fuzzy_grep" "context" context
                | None -> Ok())
            |> Result.map (fun () ->
                { pattern = patterns
                  path = strField args "path"
                  exclude = parseExcludeField args
                  searchIgnored = optBool args "searchIgnored"
                  caseSensitive = optBool args "caseSensitive"
                  context = context
                  limit = limit }))

let decodeFuzzyContinueArgs (args: obj) : Result<FuzzyContinueParams, DomainError> =
    match strField args "iterator" with
    | Some it when it.Trim() <> "" -> Ok { iterator = it }
    | _ -> Error(InvalidIntent("fuzzy_continue", "iterator", "iterator is required and cannot be empty"))
