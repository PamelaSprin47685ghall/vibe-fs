module Wanxiangshu.Tests.IntegrationMuxFallbackSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.Fallback.Hook
open Wanxiangshu.Runtime.NudgeRuntimeMux
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.NudgeOutcomeHandler
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.Plugin

/// One-shot deferred signal resolved after the first nudge text is recorded.
let private buildNudgeSignal () : (JS.Promise<unit> * (unit -> unit)) =
    let resolver = ref (fun () -> ())
    let p = Promise.create (fun resolve _ -> resolver.Value <- resolve)
    p, (fun () -> resolver.Value())

/// Build Mux deps whose nudge resolves the one-shot signal after recording.
let private buildMuxDeps
    (sessionID: string)
    (directory: string)
    (getChatHistory: string -> JS.Promise<obj array>)
    (nudges: ResizeArray<string>)
    (resolveSignal: unit -> unit)
    : obj =

    let nudgeFn =
        System.Func<obj, obj, JS.Promise<bool>>(fun _ msg ->
            promise {
                nudges.Add(string msg)
                resolveSignal ()
                return true
            })

    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
          "getChatHistory", box getChatHistory
          "nudge", box nudgeFn
          "directory", box directory ]

/// Fallback from retryable 429 error fires exactly one nudge.
let muxSessionErrorTriggersFallbackContinueSpec () =
    promise {
        let! tmpDir = mkdtempAsync "mux-fb-error-"

        do!
            writeFileAsync
                (tmpDir + "/AGENTS.md")
                "---\nmodels:\n  default:\n    - openai/gpt-5\n    - anthropic/claude-4\n---\n"

        let sessionID = "mux-fb-error-ws"
        let nudges = ResizeArray<string>()
        let nudgeObserved, resolveSignal = buildNudgeSignal ()

        let deps =
            buildMuxDeps
                sessionID
                tmpDir
                (fun sid -> promise { return if sid = sessionID then [||] else [||] })
                nudges
                resolveSignal

        let reg = createRegistration deps
        let eventHook = get reg "eventHook"

        let helpers =
            createObj [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] })) ]

        let ev =
            createObj
                [ "type", box "session.error"
                  "workspaceId", box sessionID
                  "properties",
                  box (
                      createObj
                          [ "errorType", box "APIError"
                            "statusCode", box "429"
                            "isRetryable", box "true" ]
                  ) ]

        do! (eventHook $ (ev, helpers)) |> unbox<JS.Promise<unit>>
        do! withTimeout nudgeObserved
        check "exactly one nudge dispatched" (nudges.Count = 1)
        check "nudge text contains 'continue openai/gpt-5'" (nudges.[0].Contains "continue openai/gpt-5")
        do! rmAsync tmpDir
    }

/// Fallback from tool-call-as-text recovery fires exactly one nudge.
let muxStreamEndToolCallAsTextTriggersFallbackSpec () =
    promise {
        let! tmpDir = mkdtempAsync "mux-fb-toolcall-"

        do!
            writeFileAsync
                (tmpDir + "/AGENTS.md")
                "---\nmodels:\n  default:\n    - openai/gpt-5\n    - anthropic/claude-4\n---\n"

        let sessionID = "mux-fb-toolcall-ws"
        let nudges = ResizeArray<string>()
        let nudgeObserved, resolveSignal = buildNudgeSignal ()

        let assistantMsg =
            createObj
                [ "info", box (createObj [ "role", box "assistant" ])
                  "parts",
                  box
                      [| box
                             {| ``type`` = "text"
                                text = "<tool_call><name>read</name> 后续" |} |] ]

        let getHistory sid =
            promise { return if sid = sessionID then [| assistantMsg |] else [||] }

        let deps = buildMuxDeps sessionID tmpDir getHistory nudges resolveSignal
        let reg = createRegistration deps
        let eventHook = get reg "eventHook"

        let helpers =
            createObj [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] })) ]

        let ev =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties",
                  box (
                      createObj
                          [ "metadata", box (createObj [ "muxStopReason", box "tool_use_error" ])
                            "parts",
                            box
                                [| box
                                       {| ``type`` = "text"
                                          text = "incomplete" |} |] ]
                  ) ]

        do! (eventHook $ (ev, helpers)) |> unbox<JS.Promise<unit>>
        do! withTimeout nudgeObserved
        check "exactly one nudge fires" (nudges.Count = 1)

        check
            "nudge contains FallbackMessageCodec recovery prompt"
            (nudges.[0].Contains "You produced the tool call as raw text")

        do! rmAsync tmpDir
    }

let muxAbortRunThrowsAbortUnavailableSpec () =
    promise {
        let helpers = createObj []
        let executor = muxActionExecutor helpers

        let! caught = executor.AbortRun("session-abort-test") |> Promise.result

        match caught with
        | Error ex -> check "throws AbortUnavailable exception" (ex.Message.Contains("AbortUnavailable"))
        | Ok _ -> failwith "expected AbortRun to fail"
    }

let muxNudgeBooleanTrueReturnsAcceptanceUnknownSpec () =
    promise {
        let helpers =
            createObj
                [ "nudge",
                  box (
                      System.Func<obj, obj, obj, obj, obj, obj, JS.Promise<bool>>(fun _ _ _ _ _ _ ->
                          promise { return true })
                  ) ]

        let runtime = FallbackRuntimeStore()
        let! outcome = sendNudgeMux runtime helpers "session-nudge-test" "nudge text" None None "n1" "nonce1"

        match outcome with
        | SendOutcome.AcceptanceUnknown msg -> check "returns AcceptanceUnknown" (msg.Contains("nudge resolved true"))
        | other -> failwith ("expected AcceptanceUnknown, got " + string other)
    }

let muxNudgeValidReceiptReturnsDeliveredSpec () =
    promise {
        let helpers =
            createObj
                [ "nudge",
                  box (
                      System.Func<obj, obj, obj, obj, obj, obj, JS.Promise<obj>>(fun _ _ _ _ _ _ ->
                          promise {
                              return
                                  box (
                                      createObj
                                          [ "messageId", box "msg-123"
                                            "sessionId", box "session-nudge-test"
                                            "dispatchId", box "nonce1" ]
                                  )
                          })
                  ) ]

        let runtime = FallbackRuntimeStore()
        let! outcome = sendNudgeMux runtime helpers "session-nudge-test" "nudge text" None None "n1" "nonce1"

        match outcome with
        | SendOutcome.Delivered -> check "nudge with valid receipt returns Delivered" true
        | other -> failwith ("expected Delivered, got " + string other)
    }

let muxNudgeMismatchedReceiptReturnsFailedSpec () =
    promise {
        let helpers =
            createObj
                [ "nudge",
                  box (
                      System.Func<obj, obj, obj, obj, obj, obj, JS.Promise<obj>>(fun _ _ _ _ _ _ ->
                          promise {
                              return
                                  box (
                                      createObj
                                          [ "messageId", box "msg-123"
                                            "sessionId", box "wrong-session"
                                            "dispatchId", box "nonce1" ]
                                  )
                          })
                  ) ]

        let runtime = FallbackRuntimeStore()
        let! outcome = sendNudgeMux runtime helpers "session-nudge-test" "nudge text" None None "n1" "nonce1"

        match outcome with
        | SendOutcome.Failed msg -> check "nudge with wrong session returns Failed" (msg.Contains("sessionId mismatch"))
        | other -> failwith ("expected Failed, got " + string other)
    }

let muxContinueBooleanTrueRejectsAcceptanceUnknownSpec () =
    promise {
        let helpers =
            createObj
                [ "nudge",
                  box (
                      System.Func<obj, obj, obj, obj, obj, obj, JS.Promise<bool>>(fun _ _ _ _ _ _ ->
                          promise { return true })
                  ) ]

        let executor = muxActionExecutor helpers

        let model =
            { ProviderID = "openai"
              ModelID = "gpt-4"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        let! caught =
            executor.SendContinue("session-continue-test", model, "continuation-id")
            |> Promise.result

        match caught with
        | Error ex -> check "continue rejects with AcceptanceUnknown" (ex.Message.Contains("AcceptanceUnknown"))
        | Ok _ -> failwith "expected SendContinue to reject"
    }

let muxContinueValidReceiptResolvesSpec () =
    promise {
        let helpers =
            createObj
                [ "nudge",
                  box (
                      System.Func<obj, obj, obj, obj, obj, obj, JS.Promise<obj>>(fun _ _ _ _ _ _ ->
                          promise {
                              return
                                  box (
                                      createObj
                                          [ "messageId", box "msg-456"
                                            "sessionId", box "session-continue-test"
                                            "dispatchId", box "continuation-id" ]
                                  )
                          })
                  ) ]

        let executor = muxActionExecutor helpers

        let model =
            { ProviderID = "openai"
              ModelID = "gpt-4"
              Variant = None
              Temperature = None
              TopP = None
              MaxTokens = None
              ReasoningEffort = None
              Thinking = false }

        do! executor.SendContinue("session-continue-test", model, "continuation-id")
        check "continue with valid receipt resolves successfully" true
    }

let muxAbortUnavailableNudgeFlowSpec () =
    promise {
        let! tmpDir = mkdtempAsync "mux-abort-unavail-"

        let runtime = FallbackRuntimeStore()
        let sessionKey = "test-session-abort"
        
        let _ = runtime.GetOrCreateState(sessionKey)

        let lease: NudgeLease =
            { NudgeID = "nudge-1"
              NudgeOrdinal = 1
              Nonce = "nonce-1"
              HumanTurnID = "ht-1"
              HostUserMessageId = ""
              SessionGeneration = 0
              CancelGeneration = 0
              Owner = SessionOwner.Human
              Status = LeaseStatus.Requested }

        let abortRunCalledCount = ref 0
        let abortRun _ =
            promise {
                abortRunCalledCount.Value <- abortRunCalledCount.Value + 1
                return! Promise.reject (System.Exception("AbortUnavailable: Mux host adapter does not expose a session-level abort API"))
            }

        do! Wanxiangshu.Runtime.NudgeOutcomeHandler.validateAndFinalizeOutcome tmpDir runtime sessionKey lease NudgeAction.NudgeNone "anchor" SendOutcome.Delivered abortRun

        check "abortRun was called once" (abortRunCalledCount.Value = 1)
        
        let session = runtime.GetSession sessionKey
        check "session.AbortUnavailable is true" session.AbortUnavailable

        do! Wanxiangshu.Runtime.NudgeOutcomeHandler.validateAndFinalizeOutcome tmpDir runtime sessionKey lease NudgeAction.NudgeNone "anchor" SendOutcome.Delivered abortRun
        
        check "abortRun was NOT called a second time" (abortRunCalledCount.Value = 1)

        do! rmAsync tmpDir
    }

let private defaultFallbackModel =
    { ProviderID = "openai"
      ModelID = "gpt-4"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let muxNudgeMissingHelpersReturnsFailedSpec () =
    promise {
        let runtime = FallbackRuntimeStore()

        let! outcome =
            sendNudgeMux runtime null "session-missing-helpers" "text" None None "n1" "nonce1"

        match outcome with
        | SendOutcome.Failed msg -> check "missing helpers returns Failed" (msg.Contains("helpers missing"))
        | other -> failwith ("expected Failed, got " + string other)
    }

let muxNudgeMissingNudgeReturnsFailedSpec () =
    promise {
        let runtime = FallbackRuntimeStore()
        let helpers = createObj []

        let! outcome =
            sendNudgeMux runtime helpers "session-missing-nudge" "text" None None "n1" "nonce1"

        match outcome with
        | SendOutcome.Failed msg -> check "missing nudge returns Failed" (msg.Contains("helpers.nudge missing"))
        | other -> failwith ("expected Failed, got " + string other)
    }

let muxNudgeNonFunctionNudgeReturnsFailedSpec () =
    promise {
        let runtime = FallbackRuntimeStore()
        let helpers = createObj [ "nudge", box 42 ]

        let! outcome =
            sendNudgeMux runtime helpers "session-nonfunction-nudge" "text" None None "n1" "nonce1"

        match outcome with
        | SendOutcome.Failed msg -> check "nonfunction nudge returns Failed" (msg.Contains("helpers.nudge is not a function"))
        | other -> failwith ("expected Failed, got " + string other)
    }

let muxContinueMissingHelpersRejectsFailedSpec () =
    promise {
        let executor = muxActionExecutor null

        let! caught =
            executor.SendContinue("session-continue-missing-helpers", defaultFallbackModel, "continuation-id")
            |> Promise.result

        match caught with
        | Error ex -> check "continue missing helpers rejects Failed" (ex.Message.Contains("Failed: helpers missing"))
        | Ok _ -> failwith "expected SendContinue to reject"
    }

let muxContinueMissingNudgeRejectsFailedSpec () =
    promise {
        let executor = muxActionExecutor (createObj [])

        let! caught =
            executor.SendContinue("session-continue-missing-nudge", defaultFallbackModel, "continuation-id")
            |> Promise.result

        match caught with
        | Error ex -> check "continue missing nudge rejects Failed" (ex.Message.Contains("Failed: helpers.nudge missing"))
        | Ok _ -> failwith "expected SendContinue to reject"
    }

let muxContinueNonFunctionNudgeRejectsFailedSpec () =
    promise {
        let executor = muxActionExecutor (createObj [ "nudge", box 42 ])

        let! caught =
            executor.SendContinue("session-continue-nonfunction-nudge", defaultFallbackModel, "continuation-id")
            |> Promise.result

        match caught with
        | Error ex -> check "continue nonfunction nudge rejects Failed" (ex.Message.Contains("Failed: helpers.nudge is not a function"))
        | Ok _ -> failwith "expected SendContinue to reject"
    }

let muxContinueMismatchedReceiptRejectsFailedSpec () =
    promise {
        let helpers =
            createObj
                [ "nudge",
                  box (
                      System.Func<obj, obj, obj, obj, obj, obj, JS.Promise<obj>>(fun _ _ _ _ _ _ ->
                          promise {
                              return
                                  box (
                                      createObj
                                          [ "messageId", box "msg-123"
                                            "sessionId", box "wrong-session"
                                            "dispatchId", box "continuation-id" ]
                                  )
                          })
                  ) ]

        let executor = muxActionExecutor helpers

        let! caught =
            executor.SendContinue("session-continue-mismatched", defaultFallbackModel, "continuation-id")
            |> Promise.result

        match caught with
        | Error ex ->
            check
                "continue mismatched receipt rejects Failed"
                (ex.Message.Contains("Failed:") && ex.Message.Contains("sessionId mismatch"))
        | Ok _ -> failwith "expected SendContinue to reject"
    }

let muxRecoverWithPromptMissingNudgeRejectsFailedSpec () =
    promise {
        let executor = muxActionExecutor (createObj [])

        let! caught =
            executor.RecoverWithPrompt(
                "session-recover-missing-nudge",
                defaultFallbackModel,
                "recovery prompt",
                "continuation-id"
            )
            |> Promise.result

        match caught with
        | Error ex ->
            check
                "recover with prompt missing nudge rejects Failed"
                (ex.Message.Contains("Failed: helpers.nudge missing"))
        | Ok _ -> failwith "expected RecoverWithPrompt to reject"
    }
