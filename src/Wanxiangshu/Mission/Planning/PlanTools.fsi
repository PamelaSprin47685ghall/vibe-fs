namespace Wanxiangshu.Mission.Planning

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

type DeliverResult =
    { Ok: bool
      Relayed: bool
      Delivered: bool
      RetiredIncumbencyId: string
      RetirementOutcome: string
      Digest: string
      Path: string
      ExecSessionId: string
      NextIncumbencyId: string
      Message: string }

/// planning-010: the outcome of one synchronous ask delegation.
type AskResult =
    { Kind: string
      WorkRecord: string
      Status: string }

type DeliverySpawnOutcome =
    | Started of sessionId: string
    | NotStarted of reason: string
    | Unknown of reason: string

module PlanTools =

    /// The one plan artifact read used by deliver and by the plan surface: the
    /// content, its digest, and the resolved path. Empty or missing is an error.
    val readAndValidatePlan: root: string -> workId: string -> Result<string * string * string, string>

    /// planning-007 / planning-008: deliver splits on authorization — an
    /// authorized tenure takes the terminal delivery path (spawn + retire +
    /// PlanDelivered), an unauthorized one relays to the next incumbency.
    /// `spawn` reports whether the parentless manager execution was admitted,
    /// definitely not started, or remains acceptance-unknown (planning-023).
    val executeDeliver:
        gitCommonDir: string ->
        store: IEventStore ->
        root: string ->
        workId: string ->
        incumbencyId: string ->
        providerRunId: string ->
        toolCallId: string ->
        hasBlocker: bool ->
        spawn: (string -> string -> Task<DeliverySpawnOutcome>) ->
        retirementCursor: int64 ->
            Task<Result<DeliverResult, string>>

    /// planning-010: delegate the intent synchronously to the bound fixed
    /// DevOps and return its complete work record verbatim. `delegateTo`
    /// carries the real delegation capability (injected by the tool surface);
    /// at most one pending delegation per work, tracked in process memory.
    val executeAsk:
        gitCommonDir: string ->
        store: IEventStore ->
        workId: string ->
        intent: string ->
        delegateTo: (string -> Task<string>) ->
            Task<Result<AskResult, string>>

    /// planning-011: js-plan operates on the single derived plan path with
    /// read / edit / rewrite.
    val executeJsPlan:
        root: string ->
        workId: string ->
        action: string ->
        content: string option ->
        patches: (string * string) list option ->
            Result<string, string>
