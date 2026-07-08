module Wanxiangshu.Tests.SearchToolsTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Opencode.SearchTools
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.Dyn

[<Emit("$0.execute($1,$2)")>]
let private callExecute (tool: obj) (args: obj) (context: obj) : JS.Promise<string> = jsNative

let private finderCache = FinderCache()
let private iteratorStore = createTypedIteratorStore 50
let private findTool = fuzzyFindTool finderCache iteratorStore
let private grepTool = fuzzyGrepTool finderCache iteratorStore

let private noSessionContext: obj = createObj [||]

let private validSessionContext: obj =
    createObj [ "sessionID", box "test-session-1"; "directory", box "/tmp/test" ]

let private emptyArgs: obj = createObj [||]

let fuzzyFindTool_requiresActiveSession () =
    promise {
        let! result = callExecute findTool emptyArgs noSessionContext
        check "contains tool name" (result.Contains "fuzzy_find")
        check "contains requires session" (result.Contains "requires an active session")
    }

let fuzzyFindTool_decodeFailure () =
    promise {
        let! result = callExecute findTool emptyArgs validSessionContext
        check "contains tool name" (result.Contains "fuzzy_find")
        check "decode failure mentions tool" (result.Contains "fuzzy_find" && result.Contains "failed")
    }

let fuzzyGrepTool_requiresActiveSession () =
    promise {
        let! result = callExecute grepTool emptyArgs noSessionContext
        check "contains tool name" (result.Contains "fuzzy_grep")
        check "contains requires session" (result.Contains "requires an active session")
    }

let fuzzyGrepTool_decodeFailure () =
    promise {
        let! result = callExecute grepTool emptyArgs validSessionContext
        check "decode failure mentions tool" (result.Contains "fuzzy_grep" && result.Contains "failed")
    }

let run () =
    promise {
        do! fuzzyFindTool_requiresActiveSession ()
        do! fuzzyFindTool_decodeFailure ()
        do! fuzzyGrepTool_requiresActiveSession ()
        do! fuzzyGrepTool_decodeFailure ()
    }
