namespace Wanxiangshu.Interaction.Dispatch

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.TerminalSignal
open Wanxiangshu.Host.Ports

type IDispatchSessionPort =
    abstract SendPrompt:
        sessionId: SessionId * request: PromptRequest * metadata: obj option * late: LateDeliveryListener option ->
            Task<SendOutcome>

    abstract SubscribeFutureTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable


/// Narrow adapter: the real `ISessionHostPort` is a superset; this port only
/// observes send/terminal: durable-side failures arrive back inside SendOutcome
/// or DetachedSendListener verdicts, never through a process-level reporter.
///
/// The send contract is the host-neutral P-02 shape (`PromptRequest` plus a
/// product-owned metadata side channel plus a `LateDeliveryListener`). The legacy
/// `SessionPromptOptions` only exists at the host boundary, where the adapters
/// below translate the neutral request into it.
[<RequireQualifiedAccess>]
module DispatchSessionPort =

    /// Legacy host verdict -> neutral delivery verdict.
    let toDeliveryVerdict (verdict: DetachedSendVerdict) : DeliveryVerdict =
        match verdict with
        | DetachedSendVerdict.OwnedSettled -> DeliveryVerdict.OwnedSettled
        | DetachedSendVerdict.Refused reason -> DeliveryVerdict.Refused reason
        | DetachedSendVerdict.OutcomeUnknown reason -> DeliveryVerdict.OutcomeUnknown reason

    /// Neutral delivery verdict -> legacy host verdict shape.
    let toDetachedVerdict (verdict: DeliveryVerdict) : DetachedSendVerdict =
        match verdict with
        | DeliveryVerdict.OwnedSettled -> DetachedSendVerdict.OwnedSettled
        | DeliveryVerdict.Refused reason -> DetachedSendVerdict.Refused reason
        | DeliveryVerdict.OutcomeUnknown reason -> DetachedSendVerdict.OutcomeUnknown reason

    /// Build the legacy host send options from the neutral P-02 request.
    ///
    /// `metadata` is a product-owned side channel (the neutral `PromptRequest`
    /// has no metadata slot): it carries the prompt key, the origin label and
    /// the logical run id that the host-side codecs read back.
    let toHostOptions
        (request: PromptRequest)
        (metadata: obj option)
        (late: LateDeliveryListener option)
        : SessionPromptOptions =
        { Model = None
          Agent = request.Role |> Option.map (fun (AgentRole role) -> role)
          Directory = request.Workspace |> Option.map (fun (WorkspacePath path) -> path)
          Metadata = metadata
          Tools = request.ToolPolicy
          DetachedListener =
            late
            |> Option.map (fun listener -> fun (verdict: DetachedSendVerdict) -> listener (toDeliveryVerdict verdict)) }

    let ofSessionPort (sessionPort: ISessionHostPort) : IDispatchSessionPort =
        { new IDispatchSessionPort with
            member _.SendPrompt(sessionId, request, metadata, late) =
                sessionPort.SendPrompt(sessionId, request.Text, toHostOptions request metadata late)

            member _.SubscribeFutureTerminal(sessionId, listener) =
                sessionPort.SubscribeFutureTerminal(sessionId, listener)

            member _.SubscribeTerminal(sessionId, listener) =
                sessionPort.SubscribeTerminal(sessionId, listener) }
