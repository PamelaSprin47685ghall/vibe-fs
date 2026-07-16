module Wanxiangshu.Tests.OpencodeFallbackChildIdleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.CommandProcessor
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.SubsessionActorRegistry
open Wanxiangshu.Shell.SubsessionEventStore
open Wanxiangshu.Opencode.FallbackHooks

let private fail message = check message false

let private model: FallbackModel =
    { ProviderID = "test"
      ModelID = "investigator"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private config: FallbackConfig =
    { DefaultChain = [ model ]
      AgentChains = Map.empty
      MaxRetries = 0
      LoopMaxContinues = 0
      MaxRecoveries = 0 }

type private ImmediateHost() =
    interface ISubsessionHost with
        member _.Dispatch(_, _) =
            Promise.lift (Ok OrderedTurnMarkerObserved)

        member _.Abort(_, _) = Promise.lift ConfirmedStopped
        member _.CancelPendingDispatch(_) = ()
        member _.QueryDispatchStatus(_, _) = Promise.lift DispatchStatus.Unknown
        member _.QuerySessionQuiescence(_, _) = Promise.lift Stopped
        member _.ClosePhysicalSession(_) = Promise.lift Stopped

type private TurnStartedStore() =
    let inner = MemorySubsessionEventStore()
    let mutable resolveTurnStarted = fun () -> ()

    let turnStarted = Promise.create (fun resolve _ -> resolveTurnStarted <- resolve)

    member _.WaitForTurnStarted() = turnStarted

    interface ISubsessionEventStore with
        member _.Append(sessionId, events) =
            promise {
                do! (inner :> ISubsessionEventStore).Append(sessionId, events)

                if
                    events
                    |> List.exists (function
                        | TurnStarted _ -> true
                        | _ -> false)
                then
                    resolveTurnStarted ()
            }

let currentUserNonceAnchorsAssistantEvidenceBeforeIdle () =
    promise {
        let workspaceRoot = "/opencode-fallback-current-turn-evidence"
        let sessionId = "child-opencode-current-turn-evidence"
        let runId = RunId.create "run-opencode-current-turn-evidence"
        let turnId = TurnId.create (RunId.value runId + "-t0")

        let expectedReport =
            "investigator report: the current turn found src/Opencode/FallbackHooks.fs"

        let store = TurnStartedStore()

        let actor =
            SubsessionActorRegistry.GetOrCreate
                workspaceRoot
                sessionId
                (ImmediateHost())
                (store :> ISubsessionEventStore)

        let request =
            { RunId = runId
              SessionId = SessionId.create sessionId
              ParentSessionId = SessionId.create "parent-opencode-current-turn-evidence"
              Prompt = "investigate"
              FallbackConfig = config
              Directive = RetryChain [ model ]
              InitiallyCancelled = false }

        let caller = actor.StartRun request
        do! store.WaitForTurnStarted()

        // Queue a no-op evidence observation behind DispatchAccepted. This is an
        // event-driven barrier proving the actor is Running without a timed wait.
        do!
            actor.Post(
                EvidenceUpdated
                    { TurnId = Some turnId
                      Evidence = CurrentTurnEvidence.empty }
            )

        match actor.GetState() with
        | Running _ -> ()
        | other -> fail ("expected registered child actor to be Running, got " + string other)

        let messages =
            [| box
                   {| id = "stale-user-message"
                      info = box {| role = "user" |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "earlier request"
                                 metadata = box {| nonce = "stale-turn" |} |} |] |}
               box
                   {| id = "stale-assistant-message"
                      info = box {| role = "assistant" |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "stale report that must be excluded" |} |] |}
               box
                   {| id = "current-user-message"
                      info = box {| role = "user" |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "investigate current turn"
                                 metadata = box {| nonce = TurnId.value turnId |} |} |] |}
               box
                   {| id = "current-assistant-message"
                      info = box {| role = "assistant" |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = expectedReport |} |] |} |]

        let mockClient =
            createObj
                [ "session", box (createObj [ "messages", box (fun _ -> Promise.lift (box {| data = messages |})) ]) ]

        let runtime = FallbackRuntimeState()
        let childRegistry = ChildAgentRegistry.Create()
        childRegistry.RegisterChildAgent(sessionId, "investigator", Some "parent-opencode-current-turn-evidence")

        let handler =
            createOpencodeFallbackHandler
                mockClient
                runtime
                (fun _ -> config)
                workspaceRoot
                childRegistry
                (createReviewStore ())
                None

        let idleEvent =
            createObj
                [ "event",
                  box (
                      createObj
                          [ "type", box "session.idle"
                            "properties", box (createObj [ "sessionID", box sessionId ]) ]
                  ) ]

        let! hookResult = handler idleEvent
        check "child idle event is consumed" hookResult.Consumed

        let! result = caller

        match result with
        | Succeeded report -> equal "caller receives only the current assistant report" expectedReport report
        | Failed(RecoveryExhausted reason) -> fail ("current assistant evidence was lost: " + reason)
        | other -> fail ("expected Succeeded with the investigator report, got " + string other)

        SubsessionActorRegistry.Remove workspaceRoot sessionId
    }

let run () =
    currentUserNonceAnchorsAssistantEvidenceBeforeIdle ()
