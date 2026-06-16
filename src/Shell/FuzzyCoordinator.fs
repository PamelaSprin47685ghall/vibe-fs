module VibeFs.Shell.FuzzyCoordinator

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.FuzzyQuery
open VibeFs.Kernel.FuzzyGrepDetect
open VibeFs.Shell.IteratorStore
open VibeFs.Kernel
open VibeFs.Shell.FuzzyFinderShell

type FuzzyFindParams = { pattern: string option; path: string option; limit: int option; iterator: string option }
type FuzzyGrepParams =
    { pattern: string option; path: string option; exclude: string list
      caseSensitive: bool option; context: int option; limit: int option; iterator: string option }
type SearchOptions = { cwd: string; scopeId: string; store: obj option }

type FuzzyFindState = { query: string; pageSize: int; pageIndex: int; externalBasePath: string option }
type FuzzyGrepState =
    { query: string; mode: string; smartCase: bool; beforeContext: int; afterContext: int
      pageSize: int; externalBasePath: string option; cursor: obj option }

type SearchOutcome = { output: string; isError: bool }

let resolveStore (opts: SearchOptions) = defaultArg opts.store globalIteratorStore

/// Parse raw exclude field (string or string array from JS tool args) into a typed string list.
let parseExcludeField (args: obj) : string list =
    let v = Dyn.get args "exclude"
    if Dyn.isNullish v then []
    elif Dyn.isArray v then v :?> obj array |> Array.map string |> List.ofArray
    else [ string v ]

/// Resolve the find search state — either from an iterator or built fresh.
let resolveFindSearchState (params': FuzzyFindParams) (opts: SearchOptions)
    : Result<FuzzyFindState, string> =
    let store = resolveStore opts
    match params'.iterator with
    | Some it ->
        match consumeIterator<FuzzyFindState> store it with
        | Some state -> Ok state
        | None -> Error $"fuzzy_find iterator error: unknown, expired, or already consumed iterator \"{it}\""
    | None ->
        match params'.pattern with
        | None | Some "" -> Error "pattern is required on the first call"
        | Some pattern ->
            let searchPath = resolveFuzzySearchPath params'.path opts.cwd
            let externalBasePath = if searchPath.external then Some searchPath.basePath else None
            Ok { query = buildQuery searchPath.pathConstraint pattern [] searchPath.basePath searchPath.external
                 pageSize = defaultArg params'.limit 30
                 pageIndex = 0
                 externalBasePath = externalBasePath }

/// Resolve the grep search state, rejecting wildcard-only patterns.
let resolveGrepSearchState (params': FuzzyGrepParams) (opts: SearchOptions)
    : Result<FuzzyGrepState, string> =
    let store = resolveStore opts
    match params'.iterator with
    | Some it ->
        match consumeIterator<FuzzyGrepState> store it with
        | Some state -> Ok state
        | None -> Error $"fuzzy_grep iterator error: unknown, expired, or already consumed iterator \"{it}\""
    | None ->
        match params'.pattern with
        | None | Some "" -> Error "pattern is required on the first call"
        | Some pattern ->
            let searchPath = resolveFuzzySearchPath params'.path opts.cwd
            let externalBasePath = if searchPath.external then Some searchPath.basePath else None
            let mode = detectGrepMode pattern
            if checkWildcardOnly pattern mode then
                Error $"Pattern '{pattern}' matches everything - fuzzy_grep needs a concrete substring or identifier."
            else
                Ok { query = buildQuery searchPath.pathConstraint pattern params'.exclude searchPath.basePath searchPath.external
                     mode = mode
                     smartCase = defaultArg params'.caseSensitive false |> not
                     beforeContext = defaultArg params'.context 0
                     afterContext = defaultArg params'.context 0
                     pageSize = defaultArg params'.limit 50
                     externalBasePath = externalBasePath
                     cursor = None }

/// Acquire a finder — a fresh one for external paths, the cached one for cwd.
let acquireFinder externalBasePath cwd =
    match externalBasePath with
    | Some basep -> createFinder basep
    | None -> getCachedFinder cwd

/// Release a finder — only external (fresh) finders are destroyed.
let releaseFinder (finder: FinderLike) externalBasePath =
    match externalBasePath with
    | Some _ -> finder.destroy()
    | None -> ()
