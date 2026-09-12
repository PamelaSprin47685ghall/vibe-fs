namespace Wanxiangshu.Interaction.Dispatch

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.OpenCode

type IDispatchSessionPort =
    abstract SendPrompt: sessionId: SessionId * text: string * opts: OpenCodePromptOptions -> Task<SendOutcome>
    abstract SubscribeFutureTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract ReportFatalDiagnostic: operation: string * fields: (string * string) list -> unit


/// Narrow adapter: the real `ISessionHostPort` is a superset; this port only
/// observes send/terminal plus durable-side fatal reporting (JournalAppendFailure
/// is a value, not a process-level abort).
[<RequireQualifiedAccess>]
module DispatchSessionPort =

    let ofSessionPort (sessionPort: ISessionHostPort) : IDispatchSessionPort =
        { new IDispatchSessionPort with
            member _.SendPrompt(sessionId, text, opts) =
                sessionPort.SendPrompt(sessionId, text, opts)

            member _.SubscribeFutureTerminal(sessionId, listener) =
                sessionPort.SubscribeFutureTerminal(sessionId, listener)

            member _.SubscribeTerminal(sessionId, listener) =
                sessionPort.SubscribeTerminal(sessionId, listener)

            member _.ReportFatalDiagnostic(operation, fields) =
                FatalProcess.trip operation (String.Join(";", fields |> List.map (fun (k, v) -> k + "=" + v))) }
