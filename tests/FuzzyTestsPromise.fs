module Wanxiangshu.Tests.FuzzyTestsPromise

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.FuzzySearch
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Fable.Core
open Fable.Core.JsInterop

let finderCacheConcurrencyRace () =
    promise {
        let mutable destroyed = false

        let fakeFinder =
            { new FinderLike with
                member _.fileSearch(_, _) = box null
                member _.grep(_, _) = box null
                member _.destroy() = destroyed <- true
                member _.isDestroyed = false }

        let mutable resolveFn = fun (_: Result<FinderLike, string>) -> ()
        let p = Promise.create (fun resolve reject -> resolveFn <- resolve)
        let mockCreate (cwd: string) : JS.Promise<Result<FinderLike, string>> = p
        let cache = FinderCache(mockCreate)
        let getPromise = cache.Get("/tmp")
        let destroyPromise = cache.Destroy("/tmp")
        resolveFn (Ok fakeFinder)
        let! _ = getPromise
        do! destroyPromise
        check "pending finder was destroyed on concurrency race" destroyed
        let instancesMap: Map<string, FinderLike> = (box cache)?instances
        check "instances does not contain the path" (not (Map.containsKey "/tmp" instancesMap))
    }

let grepMaxMatchesPerFileRespectsPageSize () =
    promise {
        let mutable capturedOpts: Option<obj> = None

        let mockCreate (_: string) : JS.Promise<Result<FinderLike, string>> =
            promise {
                let finder =
                    { new FinderLike with
                        member _.fileSearch(_, _) = box null

                        member _.grep(_, opts) =
                            capturedOpts <- Some opts

                            box
                                {| ok = true
                                   value = box {| items = [||]; totalMatched = 0 |} |}

                        member _.destroy() = ()
                        member _.isDestroyed = false }

                return Ok finder
            }

        let store = createTypedIteratorStore 10
        let cache = FinderCache(mockCreate)

        let opts: SearchOptions =
            { cwd = "."
              scopeId = "scope"
              store = Some store
              finderCache = cache }

        let params': FuzzyGrepParams =
            { pattern = [ "q" ]
              path = None
              exclude = []
              searchIgnored = None
              caseSensitive = None
              context = None
              limit = Some 100 }

        let! _ = searchFuzzyContent params' opts

        match capturedOpts with
        | Some o ->
            let v = Dyn.get o "maxMatchesPerFile"
            equal "maxMatchesPerFile cap removed" 100 (unbox<int> v)
        | None -> check "grep was called" false
    }

let findPagingWhenTotalMatchedIsNone () =
    promise {
        let mockCreate (_: string) : JS.Promise<Result<FinderLike, string>> =
            promise {
                let finder =
                    { new FinderLike with
                        member _.fileSearch(_, _) =
                            let items = Array.init 30 (fun i -> box {| relativePath = $"f{i}.ts" |})

                            box
                                {| ok = true
                                   value =
                                    box
                                        {| items = items
                                           totalMatched = null
                                           totalFiles = 0 |} |}

                        member _.grep(_, _) = box null
                        member _.destroy() = ()
                        member _.isDestroyed = false }

                return Ok finder
            }

        let store = createTypedIteratorStore 10
        let cache = FinderCache(mockCreate)

        let opts: SearchOptions =
            { cwd = "."
              scopeId = "scope"
              store = Some store
              finderCache = cache }

        let params': FuzzyFindParams =
            { pattern = [ "q" ]
              path = None
              limit = Some 30 }

        let! outcome = locateFuzzyMatches params' opts
        check "iterator generated when totalMatched=None at page boundary" (outcome.output.Contains "iterator:")
    }

let grepMultiPropagatesErrorAndSafety () =
    promise {
        let mutable destroyed = false
        let mutable callCount = 0

        let mockCreate (_: string) : JS.Promise<Result<FinderLike, string>> =
            promise {
                let finder =
                    { new FinderLike with
                        member _.fileSearch(_, _) = box null

                        member _.grep(query, _) =
                            callCount <- callCount + 1

                            if query.Contains "fail" then
                                failwith "intentional grep failure"
                            else
                                box
                                    {| ok = true
                                       value = box {| items = [||]; totalMatched = 0 |} |}

                        member _.destroy() = destroyed <- true
                        member _.isDestroyed = false }

                return Ok finder
            }

        let store = createTypedIteratorStore 10
        let cache = FinderCache(mockCreate)

        let opts: SearchOptions =
            { cwd = "."
              scopeId = "scope"
              store = Some store
              finderCache = cache }

        let params': FuzzyGrepParams =
            { pattern = [ "success"; "fail" ]
              path = Some "/external/path"
              exclude = []
              searchIgnored = None
              caseSensitive = None
              context = None
              limit = Some 100 }

        let! outcome = searchFuzzyContent params' opts
        check "multi grep isError is true" outcome.isError
        check "success subpattern was called" (callCount >= 1)
        check "finder was destroyed on error" destroyed
    }

let run () =
    promise {
        do! finderCacheConcurrencyRace ()
        do! grepMaxMatchesPerFileRespectsPageSize ()
        do! findPagingWhenTotalMatchedIsNone ()
        do! grepMultiPropagatesErrorAndSafety ()
    }
