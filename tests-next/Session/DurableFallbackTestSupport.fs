namespace Wanxiangshu.Next.Tests.SessionTests

open System
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// Test-only durable failure driver. Production writes only via RetrySignalHandler.
module DurableFallbackTestSupport =
    let mutable private seq = 0

    let recordFailure
        (journalPort: FallbackJournalPort)
        (sessionId: SessionId)
        (reason: string)
        =
        seq <- seq + 1
        let n = seq
        let fact =
            AgentFact.FallbackFailureRecorded
                {| SessionId = sessionId
                   Reason = reason
                   AssistantMessageId = sprintf "test-msg-%s-%d" (SessionId.value sessionId) n
                   ProviderAttempt = sprintf "test-attempt-%d" n |}

        match journalPort.AppendFact (StreamId.Session sessionId) fact with
        | Ok updated -> Ok(updated, DurableFallback.nextDecision sessionId updated)
        | Error err -> Error err
