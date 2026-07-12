module Wanxiangshu.Tests.SubagentIoFallbackRecoveryTestsPart5

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackRecoveryWait
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.SubagentIo
open Wanxiangshu.Kernel.Domain

let private waitForListenerRegistered (runtime: FallbackRuntimeState) (sessionID: string) : JS.Promise<unit> =
    let rec poll () =
        promise {
            if runtime.HasListeners sessionID then
                return ()
            else
                do! yieldMicrotask ()
                return! poll ()
        }

    poll ()

let mkModel (pid: string) (mid: string) : FallbackModel =
    { ProviderID = pid
      ModelID = mid
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let mkRetryableErr () : ErrorInput =
    { ErrorName = "err"
      DomainError = Some(UnknownJsError "fail")
      Message = "fail"
      StatusCode = None
      IsRetryable = Some true }

let mkConfig () : FallbackConfig =
    { DefaultChain = []
      AgentChains = Map.empty
      MaxRetries = 2
      LoopMaxContinues = 3
      MaxRecoveries = 5 }

/// Bug regression: FallbackEventBridge.handleEvent unconditionally cleared
/// SubsessionPending at entry.  When a fallback SendContinue injected a zws
/// prompt, the subsequent session.idle event would run ScanToolCallAsText,
/// determine taskComplete=false, set Phase=Idle + consumed=false.  With
/// SubsessionPending already cleared, all gates closed and
/// isSubagentSettled returned true (PropagatedToOuter = terminal),
/// causing waitForSubagentSettle to resolve prematurely while the
/// sub session was still running.
///
/// Fix: handleEvent must NOT clear SubsessionPending — that flag is owned
/// by the caller (SubagentIo) and must persist until waitForSubagentSettle
/// returns.
let handleEvent_doesNotClearSubsessionPending () =
    promise {
        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        let rt = FallbackRuntimeState()
        let sid = "sess-sub-pending"
        rt.SetChain sid chain
        rt.SetAgentName sid "coder"
        rt.SetSubsessionPending sid true

        let translator =
            { new IEventTranslator with
                member _.TranslateError _ =
                    Some(FallbackEvent.SessionError(mkRetryableErr ()))

                member _.ExtractSessionID _ = sid
                member _.IsSessionError _ = true
                member _.IsSessionIdle _ = false
                member _.IsSessionBusy _ = false
                member _.IsNewUserMessage(_, _) = false
                member _.ExtractRoutingContext _ = None, None }

        let executor =
            { new IActionExecutor with
                member _.SendContinue(_, _) = Promise.lift ()
                member _.FetchMessages _ = Promise.lift [||]
                member _.PropagateFailure _ = Promise.lift ()
                member _.CaptureCurrentModel _ = Promise.lift None
                member _.RecoverWithPrompt(_, _, _) = Promise.lift ()
                member _.AbortRun _ = Promise.lift () }

        let lookup (_agent: string) = cfg
        let! _ = handleEvent translator rt lookup executor "" (box ()) None

        check "SubsessionPending preserved after handleEvent" (rt.IsSubsessionPending sid)
    }

/// Full integration: SubagentIo sets SubsessionPending=true, promptWithAbort
/// resolves early (admit+wake only), then a fallback event runs through
/// handleEvent.  SubsessionPending must still be true so waitForSubagentSettle
/// keeps waiting until TaskComplete is set.
let runSubagentWaitsThroughFallbackSendContinue () =
    promise {
        let rt = FallbackRuntimeState()
        let registry = ChildAgentRegistry.Create()
        let childId = "child-fb-zws"
        registry.RegisterChildAgent(childId, "coder", Some "parent-fb")

        let model = mkModel "oai" "gpt-5"
        let chain = [ model ]
        let cfg = mkConfig ()
        rt.SetChain childId chain
        rt.SetAgentName childId "coder"

        let textExtracted = ref false

        let client =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise { return box {| data = box {| id = childId |} |} })
                            )
                            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> promise { return () }))
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        textExtracted.Value <- true

                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| info = box {| role = "assistant" |}
                                                              parts =
                                                               [| box
                                                                      {| ``type`` = "text"
                                                                         text = "after-fallback" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let runP =
            runSubagentCoreResult
                rt
                registry
                client
                "coder"
                "Spawn"
                "go"
                "/tmp"
                "parent-fb"
                (box null)
                (box null)
                false
                None

        do! waitForListenerRegistered rt childId
        do! yieldMicrotask ()

        check "text not extracted while pending" (not textExtracted.Value)

        let translator =
            { new IEventTranslator with
                member _.TranslateError _ =
                    Some(FallbackEvent.SessionError(mkRetryableErr ()))

                member _.ExtractSessionID _ = childId
                member _.IsSessionError _ = true
                member _.IsSessionIdle _ = false
                member _.IsSessionBusy _ = false
                member _.IsNewUserMessage(_, _) = false
                member _.ExtractRoutingContext _ = None, None }

        let executor =
            { new IActionExecutor with
                member _.SendContinue(_, _) = Promise.lift ()
                member _.FetchMessages _ = Promise.lift [||]
                member _.PropagateFailure _ = Promise.lift ()
                member _.CaptureCurrentModel _ = Promise.lift None
                member _.RecoverWithPrompt(_, _, _) = Promise.lift ()
                member _.AbortRun _ = Promise.lift () }

        let lookup (_agent: string) = cfg

        let! _ = handleEvent translator rt lookup executor "" (box ()) None

        do! yieldMicrotask ()

        check "SubsessionPending still true after fallback event" (rt.IsSubsessionPending childId)
        check "text not extracted after fallback event" (not textExtracted.Value)

        let s = rt.GetOrCreateState childId

        rt.UpdateState
            childId
            { s with
                Lifecycle = FallbackLifecycle.TaskComplete }

        let! result = withTimeout runP

        check "text extracted after TaskComplete" textExtracted.Value

        match result with
        | Ok text -> check "output present" (text.Contains "after-fallback")
        | Error _ -> failwith "expected Ok"
    }

let run () =
    promise {
        do! handleEvent_doesNotClearSubsessionPending ()
        do! runSubagentWaitsThroughFallbackSendContinue ()
    }
