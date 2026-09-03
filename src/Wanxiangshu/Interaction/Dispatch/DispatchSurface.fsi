namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Dispatch-owned JavaScript boundary. Host ports stay opaque and durable
/// JournalHandle capabilities never cross as Fable records; only transport
/// constructors, send observations, and claim counts are plain values.
[<RequireQualifiedAccess>]
module DispatchSurface =
    val internal sessionPort: port: obj -> ISessionHostPort

    /// JS-safe controlled Host child listing for adapter proofs. The F# Result
    /// and OpenCodeChildInfo representations stay on this registered surface.
    val acceptedChild: session: string -> title: string -> agent: string -> Result<OpenCodeChildInfo list, string>

    val admittedWithReceipt: value: string -> Outcome.SendOutcome

    val admittedWithPhysicalMessage: value: string -> Outcome.SendOutcome

    val retryable: reason: string -> Outcome.SendOutcome

    val acceptanceUnknown: reason: string -> Outcome.SendOutcome

    val fatal: reason: string -> Outcome.SendOutcome

    val decodePhysicalUserMessageId: input: obj -> output: obj -> obj
    val decodeIngress: input: obj -> output: obj -> obj

    /// Seed the durable AgentOwnerRoot needed by a continuation owner. This is
    /// the same PromptFact writer used by production ingress; the returned value
    /// contains no AgentFact/union representation.
    val appendAuthorityRoot: handle: JournalHandle -> session: string -> identitySeed: obj -> Task<obj>

    val sendAgentOwnerRoot:
        port: obj -> handle: JournalHandle -> session: string -> text: string -> identitySeed: obj -> Task<obj>

    val sendAgentOwnerRootAwait:
        port: obj -> handle: JournalHandle -> session: string -> text: string -> identitySeed: obj -> Task<obj>

    val sendContinuation:
        port: obj ->
        handle: JournalHandle ->
        session: string ->
        text: string ->
        continuation: string ->
        profile: obj ->
        effectiveAgent: string ->
        awaitMode: string ->
            Task<obj>

    /// HOST-004 / DISPATCH-PROTOCOL-002: exercise the dispatch-owned final
    /// physical-send admission without exposing Quiescence internals to this
    /// package's JS tests. Crash-reconciliation proves when the admission turns
    /// stale; this surface proves that stale evidence closes the durable claim
    /// and never reaches the Host SendPrompt boundary.
    val sendIdleContinuation:
        port: obj ->
        handle: JournalHandle ->
        session: string ->
        text: string ->
        continuation: string ->
        profile: obj ->
        effectiveAgent: string ->
        physicalAdmission: obj ->
            Task<obj>

    /// PROMPT-004/005: prove one dispatched AgentOwnerRoot at a physical message
    /// boundary. The Dispatcher writes PhysicalAccepted before registering the
    /// authority profile; only the normalized profile crosses this boundary.
    val acceptAgentOwnerRoot:
        handle: JournalHandle -> session: string -> promptKey: string -> physicalMessageId: string -> Task<obj>

    /// Registered proof boundary for external ingress: the caller supplies the
    /// exact RootSelection seed, including the deliberate absence of a seed.
    val acceptHumanRootSelection:
        handle: JournalHandle -> session: string -> physicalMessageId: string -> identitySeed: obj -> Task<obj>

    val acceptManagedExternal:
        handle: JournalHandle -> session: string -> physicalMessageId: string -> agent: string -> Task<obj>

    val acceptManagedPromptClaim:
        handle: JournalHandle ->
        session: string ->
        physicalMessageId: string ->
        promptKey: string ->
        agent: string ->
            Task<obj>

    /// PROMPT-004: accept the external HumanRoot through the same Dispatcher
    /// writer used by chat.message. The physical id is supplied by the caller as
    /// host-boundary evidence; this surface never invents an alias.
    val acceptHumanRoot:
        handle: JournalHandle -> session: string -> physicalMessageId: string -> agent: string -> Task<obj>

    val projectionObservation: handle: JournalHandle -> session: string -> obj

    val sendMemberObservation: unit -> obj

    val awaitModeObservation: unit -> obj

    val runtimeStartPolicy: unit -> obj

    val foldRuntimeStartWatermark: events: obj array -> obj

    val pendingClaimCount: handle: JournalHandle -> session: string -> int
