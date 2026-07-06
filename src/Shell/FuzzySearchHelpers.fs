module Wanxiangshu.Shell.FuzzySearchHelpers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzyIteratorStore

let parseJsonArrayOrString (v: obj) : string list =
    if Dyn.isNullish v then []
    elif Dyn.isArray v then
        (unbox<obj array> v)
        |> Array.choose (fun x -> if Dyn.isNullish x then None else Some(string x))
        |> Array.filter (fun s -> s <> "")
        |> List.ofArray
    else
        let strVal = string v
        if strVal.Trim() = "" then []
        else
            try
                let parsed = JS.JSON.parse strVal
                if Dyn.isArray parsed then
                    (unbox<obj array> parsed)
                    |> Array.choose (fun x -> if Dyn.isNullish x then None else Some(string x))
                    |> Array.filter (fun s -> s <> "")
                    |> List.ofArray
                else
                    let s = string parsed
                    if s.Trim() = "" then [] else [ s ]
            with _ ->
                [ strVal ]

let parseExcludeField (args: obj) : string list =
    let v = Dyn.get args "exclude"
    parseJsonArrayOrString v

type ResolvedGrep = { matches: GrepMatch list; total: int option; regexError: string option; cursor: obj }

type SearchOptions =
    { cwd: string
      scopeId: string
      store: TypedIteratorStore option
      finderCache: FinderCache }

let resolveStore (opts: SearchOptions) : Result<TypedIteratorStore, string> =
    match opts.store with
    | Some s -> Ok s
    | None ->
        Error "FuzzySearch requires SearchOptions.store; inject RuntimeScope.IteratorStore from the tool registration path"

let internal iteratorError toolName it = $"{toolName} iterator error: unknown, expired, or already consumed iterator \"{it}\""

let internal resolveIteratorBranch (store: TypedIteratorStore) iterator consume toolName onFresh =
    match iterator with
    | Some it when it <> "" -> match consume store it with Some s -> Ok s | None -> Error (iteratorError toolName it)
    | _ -> onFresh ()

let acquireFinderFromOptions externalBasePath (opts: SearchOptions) =
    match externalBasePath with
    | Some basep -> opts.finderCache.Get basep
    | None -> opts.finderCache.Get opts.cwd

let releaseFinder (finder: FinderLike) externalBasePath =
    match externalBasePath with
    | Some _ -> finder.destroy()
    | None -> ()

/// Run `body` against an already-acquired finder, releasing external finders
/// even when `body` throws.  External basePath = caller-owned finder we must
/// destroy; cached basePath = pooled finder we must NOT destroy.
let runWithFinder
    (finderResult: Result<FinderLike, string>)
    (externalBasePath: string option)
    (body: FinderLike -> SearchOutcome)
    : SearchOutcome =
    match finderResult with
    | Error msg -> { output = msg; isError = true }
    | Ok finder ->
        try body finder
        finally
        match externalBasePath with
        | Some _ -> finder.destroy()
        | None -> ()

let optStr (o: obj) (key: string) : string option =
    let value = Dyn.get o key
    if Dyn.isNullish value then None else Some(string value)

let optInt (o: obj) (key: string) : int option =
    let value = Dyn.get o key
    if Dyn.isNullish value then None else Some(unbox<int> value)

let optBool (o: obj) (key: string) : bool option =
    let value = Dyn.get o key
    if Dyn.isNullish value then None else Some(unbox<bool> value)

let internal itemsOf (value: obj) : obj array =
    let items = Dyn.get value "items"
    if Dyn.isNullish items || not (Dyn.isArray items) then [||] else items :?> obj array

let stringListOf (o: obj) (key: string) : string list =
    let value = Dyn.get o key
    if Dyn.isNullish value || not (Dyn.isArray value) then [] else (value :?> obj array) |> Array.map string |> List.ofArray

let annotationOf (item: obj) : FileAnnotation option =
    let git = optStr item "gitStatus"
    let total = optInt item "totalFrecencyScore"
    let access = optInt item "accessFrecencyScore"
    if git.IsSome || total.IsSome || access.IsSome then Some { gitStatus = git; totalFrecencyScore = total; accessFrecencyScore = access } else None

let toFindMatch (item: obj) : FindMatch = { relativePath = Dyn.str item "relativePath"; annotation = annotationOf item }

let internal toGrepMatch (item: obj) : GrepMatch =
    { relativePath = Dyn.str item "relativePath"
      lineNumber = optInt item "lineNumber" |> Option.defaultValue 0
      lineContent = Dyn.str item "lineContent"
      contextBefore = stringListOf item "contextBefore"
      contextAfter = stringListOf item "contextAfter"
      annotation = annotationOf item }

let internal errorMsg (raw: obj) (fallback: string) : string =
    if Dyn.isNullish (Dyn.get raw "error") then fallback else Dyn.str raw "error"
