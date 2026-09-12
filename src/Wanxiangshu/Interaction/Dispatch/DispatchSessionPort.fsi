namespace Wanxiangshu.Interaction.Dispatch

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.OpenCode

type IDispatchSessionPort =
    abstract SendPrompt: sessionId: SessionId * text: string * opts: OpenCodePromptOptions -> Task<SendOutcome>
    abstract SubscribeFutureTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract ReportFatalDiagnostic: operation: string * fields: (string * string) list -> unit


[<RequireQualifiedAccess>]
module DispatchSessionPort =
    val ofSessionPort: sessionPort: ISessionHostPort -> IDispatchSessionPort
