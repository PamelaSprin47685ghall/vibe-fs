namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Session.DurableFallback
open Wanxiangshu.Next.Domain

/// Durable fallback failure writer. Classification of completed assistant turns
/// now happens on the typed ReconciledTurn path; raw message part decoding has
/// one owner in HostMessageCodec.
module FallbackDetect =

    /// Builds and appends the durable FallbackCursorAdvanced fact. The sole
    /// caller is RetrySignalHandler; terminal/idle paths must not reach this.
    /// Returns the updated cursor after the append so callers can see the next
    /// EffectiveAgent, but the journal fold is the single source of truth.
    let recordFallbackFailure
        (journal: AgentJournal option)
        (recorded: HashSet<string>)
        (sessionId: string)
        (logicalRunId: string)
        (authorityRootUserMessageId: string)
        (assistantMessageId: string)
        (providerAttempt: string)
        (reason: string)
        : AgentPairCursor.FallbackCursor =
        let sid = SessionId.create sessionId

        let identity =
            AgentPairCursor.failureIdentity (
                AgentPairCursor.attemptIdentity logicalRunId authorityRootUserMessageId providerAttempt
            )

        let currentCursor (j: AgentJournal) =
            DurableFallback.nextDecision sid (AgentJournal.snapshot j)

        let append () : AgentPairCursor.FallbackCursor =
            match journal with
            | None -> AgentPairCursor.initial
            | Some j ->
                let fact =
                    AgentFact.FallbackCursorAdvanced
                        {| SessionId = sid
                           LogicalRunId = logicalRunId
                           AuthorityRootUserMessageId = authorityRootUserMessageId
                           Reason = reason
                           AssistantMessageId = assistantMessageId
                           ProviderAttempt = providerAttempt |}

                match AgentJournal.appendAgent (StreamId.Session sid) None fact j with
                | Ok _ -> currentCursor j
                | Error _ ->
                    // Append failed (e.g. duplicate race): keep current cursor.
                    currentCursor j

        if recorded.Add identity then
            match journal with
            | None -> append ()
            | Some j ->
                let alreadyRecorded =
                    let projection = AgentJournal.snapshot j

                    projection.AgentProjections.Sessions
                    |> Map.tryFind sid
                    |> Option.bind (fun session -> session.Fallback)
                    |> Option.exists (fun fb -> List.contains identity fb.RecentFailureIds)

                if alreadyRecorded then currentCursor j else append ()
        else
            match journal with
            | None -> AgentPairCursor.initial
            | Some j -> currentCursor j
