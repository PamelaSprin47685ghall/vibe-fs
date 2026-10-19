namespace Wanxiangshu.Composition.Durable

open System
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Host
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Durable routing vocabulary. Concrete fact families live with their semantic
/// owners; this module only joins them into the journal's outer dispatch.
module Fact =

    /// Read-only compatibility shape for journals written while assume owned a
    /// persistent canvas. New code has no constructor or write path for it; fold
    /// treats it as a historical tombstone.
    type LegacyAssumePhaseCommitted =
        { OwnerKey: string
          SessionId: SessionId
          IncumbencyId: string
          ToolCallId: ToolCallId
          Ordinal: int64
          InputDigest: string
          PredecessorOrdinal: int64 option
          SnapshotRef: BlobRef
          SnapshotDigest: BlobDigest
          RendererVersion: string }

    [<RequireQualifiedAccess>]
    type LegacyCognitionFact = AssumePhaseCommitted of LegacyAssumePhaseCommitted

    /// Read-only marker from the retired mailbox. It must not hide later identity evidence.
    type LegacyMailboxSubscription =
        { Concern: string
          Generation: string
          Id: string
          OwnerSessionId: SessionId }

    [<RequireQualifiedAccess>]
    type LegacyConcernFact = MailboxSubscribed of LegacyMailboxSubscription

    type RuntimeFact =
        | RuntimeStarted of
            {| RuntimeId: RuntimeId
               ProcessId: int
               StartedAt: DateTimeOffset |}

    /// One journal line for the agent domain: exactly one owned family.
    /// DSL-class: DurableFact
    [<RequireQualifiedAccess>]
    type AgentFact =
        | Prompt of PromptFactCases
        | ProviderFailure of ProviderFailureFactCases
        | Execution of ExecutionFactCases
        | Orchestrator of OrchestratorFactCases
        | Companion of CompanionFactCases
        | Context of ContextFactCases
        | Host of HostFactCases
        | Fission of FissionFactCases
        | Delegation of DelegationFactCases
        | ChatExecution of ChatExecutionFactCases
        | Cognition of LegacyCognitionFact
        | Concern of LegacyConcernFact

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
