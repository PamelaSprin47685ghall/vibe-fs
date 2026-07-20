module Wanxiangshu.Tests.ContinuationPathSsotTests

open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchRegistry
open Wanxiangshu.Runtime.Fallback.ContinuationExecutionCore
open Wanxiangshu.Runtime.Fallback.ContinuationExecution
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.EventLogRuntimeStore

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private cwd () : string = unbox<string> (nodeProcess?cwd ())

let private readRelative (relativePath: string) : string =
    readFileSync (pathJoin (cwd ()) relativePath) "utf8"

let private countMatches (pattern: string) (text: string) : int =
    Regex.Matches(text, pattern).Count

let private defaultModel =
    { ProviderID = "openai"
      ModelID = "gpt-5"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private seedSession (rt: FallbackRuntimeStore) (sid: string) =
    rt.Update(sid, fun s ->
        { s with
            HumanTurnId = "turn-1"
            SessionGeneration = 1
            CancelGeneration = 0
            AgentName = "reviewer"
            Owner = SessionOwner.Fallback })
    rt.Update(sid, startDispatch defaultModel None)

/// One physical SendContinue call site in production runtime.
let singleExecutorSendContinueCallSite () =
    let core = readRelative "src/Runtime/Fallback/ContinuationExecutionCore.fs"
    let sendCalls = countMatches @"executor\.SendContinue\s*\(" core
    check "ContinuationExecutionCore has exactly one executor.SendContinue" (sendCalls = 1)

    let runtimeFallback = pathJoin (cwd ()) "src/Runtime/Fallback"
    let otherHits =
        collectFsFiles runtimeFallback
        |> List.filter (fun p -> not (p.EndsWith("ContinuationExecutionCore.fs")))
        |> List.sumBy (fun p -> countMatches @"executor\.SendContinue\s*\(" (readFileSync p "utf8"))

    check "no other Runtime/Fallback executor.SendContinue call sites" (otherHits = 0)

/// OpenCode has exactly one IActionExecutor.SendContinue implementation.
let singleOpencodeActionExecutorSendContinue () =
    let openCodeFallback = pathJoin (cwd ()) "src/Hosts/OpenCode/Fallback"
    let files = collectFsFiles openCodeFallback
    let implFiles =
        files
        |> List.filter (fun p ->
            let text = readFileSync p "utf8"
            text.Contains("member _.SendContinue") || text.Contains("member __.SendContinue"))

    check "exactly one OpenCode Fallback SendContinue member" (implFiles.Length = 1)
    check
        "SendContinue lives in ActionExecutor.fs"
        (implFiles.[0].EndsWith("ActionExecutor.fs") || implFiles.[0].Replace("\\", "/").EndsWith("ActionExecutor.fs"))

/// Deleted dual-architecture files stay gone.
let continuationHostFilesRemainDeleted () =
    let removed =
        [ "src/Runtime/Fallback/ContinuationHost.fs"
          "src/Runtime/Fallback/ContinuationCommandProcessor.fs"
          "src/Runtime/Fallback/ContinuationSupervisor.fs"
          "src/Hosts/OpenCode/Fallback/ContinuationHost.fs" ]

    for relativePath in removed do
        check
            (sprintf "removed dual-arch file stays gone: %s" relativePath)
            (not (existsSync (pathJoin (cwd ()) relativePath)))

/// handleTransportReturned must not promote to Dispatched.
let transportReturnDoesNotEqualDispatched () =
    promise {
        let rt = FallbackRuntimeStore()
        let sid = "ssot-transport"
        seedSession rt sid

        let lease0 = (rt.GetSession sid).PendingLease.Value
        rt.Update(sid, fun s ->
            { s with
                PendingLease = Some { lease0 with Status = LeaseStatus.DispatchStarted }
                Owner = SessionOwner.Fallback })

        let lease = (rt.GetSession sid).PendingLease.Value

        let executor =
            { new IActionExecutor with
                member _.SendContinue(_, _, _) = Promise.lift ()
                member _.RecoverWithPrompt(_, _, _, _) = Promise.lift ()
                member _.FetchMessages _ = Promise.lift [||]
                member _.PropagateFailure _ = Promise.lift ()
                member _.CaptureCurrentModel _ = Promise.lift None
                member _.AbortRun _ = Promise.lift () }

        do! handleTransportReturned rt executor "" sid lease defaultModel "reviewer"

        let after = (rt.GetSession sid).PendingLease.Value
        check "transport return leaves DispatchStarted" (after.Status = LeaseStatus.DispatchStarted)
        check "transport return does not set InjectedAt" (rt.GetSession sid).InjectedAt.IsNone
    }

/// Host evidence path is the sole Dispatched promotion.
let hostAcceptanceIsSoleDispatchedPath () =
    promise {
        let! root = mkdtempAsync "cont-ssot-accept-"
        let rt = FallbackRuntimeStore()
        let sid = "ssot-accept"
        seedSession rt sid

        let lease0 = (rt.GetSession sid).PendingLease.Value
        rt.Update(sid, fun s ->
            { s with
                PendingLease = Some { lease0 with Status = LeaseStatus.DispatchStarted }
                Owner = SessionOwner.Fallback })

        let cid = (rt.GetSession sid).PendingLease.Value.ContinuationID
        let! ok = recordHostAcceptedContinuation rt root sid cid
        check "host accept succeeds" ok
        check "host accept → Dispatched" ((rt.GetSession sid).PendingLease.Value.Status = LeaseStatus.Dispatched)
        check "host accept sets InjectedAt once-flag" (rt.GetSession sid).InjectedAt.IsSome

        let! ok2 = recordHostAcceptedContinuation rt root sid cid
        check "second host accept is idempotent" ok2
        check "status stays Dispatched" ((rt.GetSession sid).PendingLease.Value.Status = LeaseStatus.Dispatched)
        tryRemove root
        do! rmAsync root
    }

/// executeSendContinue is the only runtime bridge into IActionExecutor.SendContinue.
let executeSendContinueIsSingleBridge () =
    promise {
        let! root = mkdtempAsync "cont-ssot-bridge-"
        let rt = FallbackRuntimeStore()
        let sid = "ssot-bridge"
        let calls = ResizeArray<string>()
        seedSession rt sid

        let lease = (rt.GetSession sid).PendingLease.Value

        let executor =
            { new IActionExecutor with
                member _.SendContinue(sessionID, _, continuationID) =
                    promise {
                        calls.Add(sessionID + ":" + continuationID)
                    }
                member _.RecoverWithPrompt(_, _, _, _) = Promise.lift ()
                member _.FetchMessages _ = Promise.lift [||]
                member _.PropagateFailure _ = Promise.lift ()
                member _.CaptureCurrentModel _ = Promise.lift None
                member _.AbortRun _ = Promise.lift () }

        do! executeSendContinue rt executor root sid lease defaultModel "reviewer" inlineReenter

        check "exactly one physical SendContinue" (calls.Count = 1)
        check "SendContinue args match lease" (calls.[0] = sid + ":" + lease.ContinuationID)
        check
            "bridge without receipt stays DispatchStarted"
            ((rt.GetSession sid).PendingLease.Value.Status = LeaseStatus.DispatchStarted)
        tryRemove root
        do! rmAsync root
    }

let run () =
    promise {
        singleExecutorSendContinueCallSite ()
        singleOpencodeActionExecutorSendContinue ()
        continuationHostFilesRemainDeleted ()
        do! transportReturnDoesNotEqualDispatched ()
        do! hostAcceptanceIsSoleDispatchedPath ()
        do! executeSendContinueIsSingleBridge ()
    }
