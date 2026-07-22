module Wanxiangshu.Runtime.FuzzySearchGrep

open Fable.Core
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearchSupport
open Wanxiangshu.Runtime.FuzzyGrepTypes
open Wanxiangshu.Runtime.FuzzySearchGrepMatch
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.ToolOutputInfo

let private annotationText (annotation: FileAnnotation option) : string option =
    match annotation with
    | None -> None
    | Some a ->
        let text = fileAnnotation (Some a)

        if text = "" then
            None
        else
            Some(text.Trim())

let private toGrepItems (pattern: string option) (items: GrepMatch list) : FuzzyGrepMatchItem list =
    items
    |> List.map (fun m ->
        { path = m.relativePath
          line = m.lineNumber
          content = truncateLine m.lineContent grepMaxLineLength
          pattern = pattern
          contextBefore = m.contextBefore |> List.map (fun line -> truncateLine line grepMaxLineLength)
          contextAfter = m.contextAfter |> List.map (fun line -> truncateLine line grepMaxLineLength)
          annotation = annotationText m.annotation })

let private renderGrep (pattern: string option) (resolved: ResolvedGrep) (nextIterator: string) : string =
    let msg =
        { empty with
            content =
                FuzzyGrep
                    { pattern = pattern
                      totalMatched = resolved.total
                      regexFallbackError = resolved.regexError
                      matches = toGrepItems pattern resolved.matches } }

    render (withIterator msg nextIterator)

let private runGrepWithFinder
    (state: FuzzyGrepState)
    (cursor: obj option)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    (finder: FinderLike)
    : SearchOutcome =
    let raw = runGrep finder state cursor None

    if not (Dyn.truthy (Dyn.get raw "ok")) then
        { output = errorMsg raw "fuzzy_grep failed"
          isError = true }
    else
        let resolved = resolveResult raw
        let nextIterator = grepNextIterator state store opts resolved.cursor
        { output = renderGrep None resolved nextIterator; isError = false }

let private fuzzyGrepSingle (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        match resolveStore opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok store ->
            match resolveGrepIteratorState params' opts with
            | Error msg -> return { output = msg; isError = true }
            | Ok iteratorState ->
                let! finderResult = acquireFinderFromOptions iteratorState.core.externalBasePath opts

                return
                    runWithFinder
                        finderResult
                        iteratorState.core.externalBasePath
                        (runGrepWithFinder iteratorState.core iteratorState.cursor store opts)
    }

type private MultiAcc =
    { matches: ResizeArray<FuzzyGrepMatchItem>
      mutable anyError: bool
      mutable errOut: string
      mutable totalMatched: int
      mutable regexFallback: string option }

let private emptyMultiAcc () : MultiAcc =
    { matches = ResizeArray()
      anyError = false
      errOut = ""
      totalMatched = 0
      regexFallback = None }

let private appendResolved (acc: MultiAcc) (pat: string) (resolved: ResolvedGrep) : unit =
    match resolved.total with
    | Some t -> acc.totalMatched <- acc.totalMatched + t
    | None -> acc.totalMatched <- acc.totalMatched + resolved.matches.Length

    match resolved.regexError with
    | Some e when acc.regexFallback.IsNone -> acc.regexFallback <- Some e
    | _ -> ()

    for item in toGrepItems (Some pat) resolved.matches do
        acc.matches.Add item

let private collectOnePattern (finder: FinderLike) (params': FuzzyGrepParams) (opts: SearchOptions) (acc: MultiAcc) (pat: string) : unit =
    try
        match resolveGrepIteratorStateForPattern pat params' opts with
        | Error msg ->
            acc.anyError <- true
            acc.errOut <- msg
        | Ok state ->
            let raw = runGrep finder state.core None None

            if not (Dyn.truthy (Dyn.get raw "ok")) then
                acc.anyError <- true
                acc.errOut <- errorMsg raw "fuzzy_grep failed"
            else
                appendResolved acc pat (resolveResult raw)
    with ex ->
        acc.anyError <- true
        acc.errOut <- ex.Message

let private finishMulti (acc: MultiAcc) : SearchOutcome =
    if acc.anyError && acc.matches.Count = 0 then
        { output = acc.errOut; isError = true }
    else
        let msg =
            { empty with
                content =
                    FuzzyGrep
                        { pattern = None
                          totalMatched = Some acc.totalMatched
                          regexFallbackError = acc.regexFallback
                          matches = acc.matches |> Seq.toList } }

        { output = render msg; isError = acc.anyError }

let private fuzzyGrepMulti
    (patterns: string list)
    (params': FuzzyGrepParams)
    (opts: SearchOptions)
    : JS.Promise<SearchOutcome> =
    promise {
        let searchPath = resolveFuzzySearchPath params'.path opts.cwd
        let externalBasePath = if searchPath.external then Some searchPath.basePath else None
        let! finderResult = opts.finderCache.Get searchPath.basePath

        match finderResult with
        | Error msg -> return { output = msg; isError = true }
        | Ok finder ->
            try
                let acc = emptyMultiAcc ()

                for pat in patterns do
                    collectOnePattern finder params' opts acc pat

                return finishMulti acc
            finally
                if externalBasePath.IsSome then
                    opts.finderCache.Destroy searchPath.basePath |> ignore
    }

let searchFuzzyContent (params': FuzzyGrepParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    match params'.pattern with
    | [ _ ]
    | [] -> fuzzyGrepSingle params' opts
    | multi -> fuzzyGrepMulti multi params' opts

let paginateFuzzyGrepContent
    (iteratorState: GrepIteratorState)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    : JS.Promise<SearchOutcome> =
    promise {
        let! finderResult = acquireFinderFromOptions iteratorState.core.externalBasePath opts

        return
            runWithFinder
                finderResult
                iteratorState.core.externalBasePath
                (runGrepWithFinder iteratorState.core iteratorState.cursor store opts)
    }
