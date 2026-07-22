module Wanxiangshu.Runtime.FuzzySearchFind

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.FuzzySearchSupport
open Wanxiangshu.Runtime.FuzzySearchFindHelper
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.ToolOutputInfoTypes

let private annotationText (annotation: FileAnnotation option) : string option =
    match annotation with
    | None -> None
    | Some a ->
        let text = fileAnnotation (Some a)

        if text = "" then
            None
        else
            Some(text.Trim())

let private toFindItems (pattern: string option) (items: FindMatch list) : FuzzyFindMatchItem list =
    items
    |> List.map (fun m ->
        { path = m.relativePath
          pattern = pattern
          annotation = annotationText m.annotation })

let private renderFind
    (pattern: string option)
    (result: FindResult)
    (nextIterator: string)
    : string =
    let msg =
        { empty with
            content =
                FuzzyFind
                    { pattern = pattern
                      totalMatched = result.totalMatched
                      totalFiles = Some result.totalFiles
                      matches = toFindItems pattern result.items } }

    render (withIterator msg nextIterator)

let private runFind
    (state: FuzzyFindState)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    (finder: FinderLike)
    : SearchOutcome =
    let raw =
        finder.fileSearch (
            state.query,
            box
                {| pageIndex = state.pageIndex
                   pageSize = state.pageSize |}
        )

    if not (Dyn.truthy (Dyn.get raw "ok")) then
        { output = errorMsg raw "fuzzy_find failed"
          isError = true }
    else
        let value = Dyn.get raw "value"
        let _, result = processRawFindResponse value

        let totalForPaging =
            match result.totalMatched with
            | Some total -> total
            | None ->
                if result.items.Length >= state.pageSize then
                    (state.pageIndex + 2) * state.pageSize
                else
                    0

        let nextIterator = findNextIterator state store opts totalForPaging
        { output = renderFind None result nextIterator; isError = false }

let private fuzzyFindSingle (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        match resolveStore opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok store ->
            match resolveFindSearchState params' opts with
            | Error msg -> return { output = msg; isError = true }
            | Ok state ->
                let! finderResult = acquireFinderFromOptions state.externalBasePath opts
                return runWithFinder finderResult state.externalBasePath (runFind state store opts)
    }

let private collectPattern
    (finder: FinderLike)
    (params': FuzzyFindParams)
    (opts: SearchOptions)
    (pat: string)
    : Result<FindResult, string> =
    match resolveFindSearchStateForPattern pat params' opts with
    | Error msg -> Error msg
    | Ok state ->
        let raw =
            finder.fileSearch (
                state.query,
                box
                    {| pageIndex = 0
                       pageSize = state.pageSize |}
            )

        if not (Dyn.truthy (Dyn.get raw "ok")) then
            Error(errorMsg raw "fuzzy_find failed")
        else
            let value = Dyn.get raw "value"
            let _, result = processRawFindResponse value
            Ok result

let private fuzzyFindMulti
    (patterns: string list)
    (params': FuzzyFindParams)
    (opts: SearchOptions)
    : JS.Promise<SearchOutcome> =
    promise {
        let searchPath = resolveFuzzySearchPath params'.path opts.cwd

        let externalBasePath =
            if searchPath.external then
                Some searchPath.basePath
            else
                None

        let! finderResult = opts.finderCache.Get searchPath.basePath

        match finderResult with
        | Error msg -> return { output = msg; isError = true }
        | Ok finder ->
            try
                let acc = ResizeArray<FuzzyFindMatchItem>()
                let mutable anyError = false
                let mutable errOut = ""
                let mutable totalFilesAcc = 0
                let mutable totalMatchedAcc = 0

                for pat in patterns do
                    match collectPattern finder params' opts pat with
                    | Error msg ->
                        anyError <- true
                        errOut <- msg
                    | Ok result ->
                        totalFilesAcc <- max totalFilesAcc result.totalFiles

                        match result.totalMatched with
                        | Some t -> totalMatchedAcc <- totalMatchedAcc + t
                        | None -> totalMatchedAcc <- totalMatchedAcc + result.items.Length

                        for item in toFindItems (Some pat) result.items do
                            acc.Add item

                if anyError && acc.Count = 0 then
                    return { output = errOut; isError = true }
                else
                    let msg =
                        { empty with
                            content =
                                FuzzyFind
                                    { pattern = None
                                      totalMatched = Some totalMatchedAcc
                                      totalFiles = Some totalFilesAcc
                                      matches = acc |> Seq.toList } }

                    return { output = render msg; isError = anyError }
            finally
                if externalBasePath.IsSome then
                    opts.finderCache.Destroy searchPath.basePath |> ignore
    }

let locateFuzzyMatches (params': FuzzyFindParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    match params'.pattern with
    | [ _ ]
    | [] -> fuzzyFindSingle params' opts
    | multi -> fuzzyFindMulti multi params' opts

let paginateFuzzyFindMatches
    (state: FuzzyFindState)
    (store: TypedIteratorStore)
    (opts: SearchOptions)
    : JS.Promise<SearchOutcome> =
    promise {
        let! finderResult = acquireFinderFromOptions state.externalBasePath opts
        return runWithFinder finderResult state.externalBasePath (runFind state store opts)
    }
