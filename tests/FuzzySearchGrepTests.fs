module Wanxiangshu.Tests.FuzzySearchGrepTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.FuzzySearchHelpers
open Wanxiangshu.Shell.FuzzySearchGrep
open Wanxiangshu.Shell.FuzzyIteratorStore

let private fakeStore () = createTypedIteratorStore 100

let private optsWithStore () : SearchOptions =
    { cwd = "/tmp"
      scopeId = "s"
      store = Some(fakeStore ())
      finderCache = null }

// 1. noStore – resolveStore short-circuits before iterator/pattern checks
let noStore_returnsError () =
    let opts: SearchOptions =
        { cwd = "/tmp"
          scopeId = "s"
          store = None
          finderCache = null }

    let params': FuzzyGrepParams =
        { pattern = [ "x" ]
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = None
          limit = None
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Error msg -> check "noStore error mentions store" (msg.Contains "requires SearchOptions.store")
    | Ok _ -> check "noStore → Error" false

// 2. missingPattern – None or empty string is declined before wildcard/iterator logic
let missingPattern_returnsError () =
    let opts = optsWithStore ()

    let paramsNone: FuzzyGrepParams =
        { pattern = []
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = None
          limit = None
          iterator = None }

    match resolveGrepIteratorState paramsNone opts with
    | Error msg -> check "None pattern error" (msg.Contains "pattern is required")
    | Ok _ -> check "None pattern → Error" false

// 3. wildcardOnly – detectGrepMode=regex + checkWildcardOnly blocks universal patterns
let wildcardOnly_returnsError () =
    let opts = optsWithStore ()

    let params': FuzzyGrepParams =
        { pattern = [ ".*" ]
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = None
          limit = None
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Error msg -> check "wildcard error mentions matches everything" (msg.Contains "matches everything")
    | Ok _ -> check "wildcardOnly → Error" false

// 4. invalidIterator – consumeGrepIterator fails, bypasses pattern logic entirely
let invalidIterator_returnsError () =
    let opts = optsWithStore ()

    let params': FuzzyGrepParams =
        { pattern = [ "anything" ]
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = None
          limit = None
          iterator = Some "bad-iterator-id" }

    match resolveGrepIteratorState params' opts with
    | Error msg -> check "invalid iterator error" (msg.Contains "iterator")
    | Ok _ -> check "invalidIterator → Error" false

// 5. validPattern – happy path: query built, defaults filled, cursor=None
let validPattern_returnsOk () =
    let opts = optsWithStore ()

    let params': FuzzyGrepParams =
        { pattern = [ "test" ]
          path = None
          exclude = []
          searchIgnored = None
          caseSensitive = None
          context = Some 2
          limit = Some 10
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Ok state ->
        check "query non-empty" (state.core.query <> "")
        equal "mode" "plain" state.core.mode
        equal "smartCase" true state.core.smartCase
        equal "beforeContext" 2 state.core.beforeContext
        equal "afterContext" 2 state.core.afterContext
        equal "pageSize" 10 state.core.pageSize
        check "cursor None" (state.cursor = None)
    | Error msg -> check "valid → Ok" false

let searchIgnored_addsGitIgnoredConstraint () =
    let opts = optsWithStore ()

    let params': FuzzyGrepParams =
        { pattern = [ "needle" ]
          path = Some "node_modules/pkg"
          exclude = []
          searchIgnored = Some true
          caseSensitive = None
          context = None
          limit = None
          iterator = None }

    match resolveGrepIteratorState params' opts with
    | Ok state ->
        check "query contains ignored constraint" (state.core.query.Contains "git:ignored")
        check "query keeps pattern" (state.core.query.Contains "needle")
        check "query keeps path" (state.core.query.Contains "node_modules/pkg/")
    | Error _ -> check "searchIgnored → Ok" false

let run () =
    noStore_returnsError ()
    missingPattern_returnsError ()
    wildcardOnly_returnsError ()
    invalidIterator_returnsError ()
    validPattern_returnsOk ()
    searchIgnored_addsGitIgnoredConstraint ()
