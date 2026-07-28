namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module FallbackModelSelectionTests =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private retrySignal (sessionId: string) (attempt: string) (reason: string) : RetrySignal =
        { SessionId = SessionId.create sessionId
          Attempt = attempt
          Reason = reason
          MessageId = None }

    /// Same-run provider model selection is A after 0/1 failures and B after 2/3.
    let ``resolveForSession follows A A B B before dead`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-models"
                let sid = SessionId.create sessionId
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                use journal =
                    AgentJournal.create directory (RuntimeId.create "fallback-models-runtime") 1 DateTimeOffset.UtcNow

                let cfg =
                    { ModelResolver.SideA =
                        { providerID = "test"
                          modelID = "model-a"
                          variant = None }
                      ModelResolver.SideB =
                        { providerID = "test"
                          modelID = "model-b"
                          variant = None } }

                let selected () =
                    ModelResolver.resolveForSession cfg sid (AgentJournal.snapshot journal)
                    |> Option.map (fun m -> m.modelID)

                equal (Some "model-a") (selected ())

                RetrySignalHandler.handle (Some journal) recorded userBindings (retrySignal sessionId "1" "f1")
                equal (Some "model-a") (selected ())

                RetrySignalHandler.handle (Some journal) recorded userBindings (retrySignal sessionId "2" "f2")
                equal (Some "model-b") (selected ())

                RetrySignalHandler.handle (Some journal) recorded userBindings (retrySignal sessionId "3" "f3")
                equal (Some "model-b") (selected ())

                RetrySignalHandler.handle (Some journal) recorded userBindings (retrySignal sessionId "4" "f4")
                equal None (selected ())
            })
