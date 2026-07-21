module Wanxiangshu.Tests.SubagentToolExecuteTests

open Wanxiangshu.Hosts.Opencode.SubagentIoRun
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Tests.TestWorkspace

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

let private createMockCleanupClient (childId: string) (sequence: ResizeArray<string>) (failDelete: bool) =
    let sessionObj =
        createObj
            [ "create", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = box {| id = childId |} |} }))
              "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = [||] |} }))
              "abort", box (System.Func<obj, JS.Promise<obj>>(fun _ -> sequence.Add("abort"); Promise.lift (box null)))
              "delete", box (System.Func<obj, JS.Promise<obj>>(fun _ -> sequence.Add("delete"); if failDelete then Promise.reject (exn "mock delete failed") else Promise.lift (box null)))
              "prompt", box (System.Func<obj, JS.Promise<obj>>(fun _ -> Promise.lift (box null))) ]
    createObj [ "session", box sessionObj ]

let executeOpencodeCleanupSuccessSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-cleanup-success-"
        let registry = ChildAgentRegistry.Create()
        let runtime = FallbackRuntimeStore()
        let sequence = ResizeArray<string>()
        let mockClient = createMockCleanupClient "child-session-1" sequence false
        let abortedSignal = createObj [ "aborted", box true ]
        let context = createObj [ "abort", box abortedSignal ]
        let dummyHost = Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter.createHost mockClient "coder" workspaceDir
        let dummyStore = Wanxiangshu.Runtime.SubsessionEventStore.create workspaceDir
        let _ = SubsessionActorRegistry.GetOrCreate workspaceDir "child-session-1" dummyHost dummyStore

        let! result = runSubagentWithCleanup runtime registry mockClient "coder" "Coder" "prompt" workspaceDir "parent-session-1" context

        check "cleanup success result is aborted" (match result with Ok res -> res = "(aborted)" | _ -> false)
        check "only abort was called, not delete" (sequence.Count = 1 && sequence.[0] = "abort")
        check "actor is removed" (SubsessionActorRegistry.TryGet workspaceDir "child-session-1" |> Option.isNone)
        check "child agent registry is unregistered" (registry.LookupChildAgent "child-session-1" |> Option.isNone)

        let ndjsonFile = System.IO.Path.Combine(workspaceDir, ".wanxiangshu.ndjson")
        check "ndjson file exists" (System.IO.File.Exists(ndjsonFile))
        let content = readFileSync ndjsonFile "utf8"
        check "ndjson contains physical session closed event" (content.Contains("subsession_physical_session_closed"))

        do! rmAsync workspaceDir
    }

let executeOpencodeCleanupFailureSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-cleanup-failure-"
        let registry = ChildAgentRegistry.Create()
        let runtime = FallbackRuntimeStore()
        let sequence = ResizeArray<string>()
        let mockClient = createMockCleanupClient "child-session-2" sequence true
        let abortedSignal = createObj [ "aborted", box true ]
        let context = createObj [ "abort", box abortedSignal ]
        let dummyHost = Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter.createHost mockClient "coder" workspaceDir
        let dummyStore = Wanxiangshu.Runtime.SubsessionEventStore.create workspaceDir
        let _ = SubsessionActorRegistry.GetOrCreate workspaceDir "child-session-2" dummyHost dummyStore

        let! result = runSubagentWithCleanup runtime registry mockClient "coder" "Coder" "prompt" workspaceDir "parent-session-2" context

        check "cleanup result is aborted since it early returns" (match result with Ok res -> res = "(aborted)" | _ -> false)
        check "only abort was called, not delete" (sequence.Count = 1 && sequence.[0] = "abort")
        check "actor is removed" (SubsessionActorRegistry.TryGet workspaceDir "child-session-2" |> Option.isNone)
        check "child agent registry is unregistered" (registry.LookupChildAgent "child-session-2" |> Option.isNone)

        let ndjsonFile = System.IO.Path.Combine(workspaceDir, ".wanxiangshu.ndjson")
        check "ndjson file exists" (System.IO.File.Exists(ndjsonFile))
        let content = readFileSync ndjsonFile "utf8"
        check "ndjson contains physical session closed event" (content.Contains("subsession_physical_session_closed"))

        SubsessionActorRegistry.Remove workspaceDir "child-session-2"
        do! rmAsync workspaceDir
    }

let run () =
    promise {
        do! executeOpencodeCleanupSuccessSpec ()
        do! executeOpencodeCleanupFailureSpec ()
    }
