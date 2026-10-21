namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

type TerminalTracePort =
    { CaptureTerminalText:
        SessionId -> string -> ProviderRunIdentity -> Task<Result<XTraceCaptureReceipt, XTraceCaptureError>> }

[<RequireQualifiedAccess>]
module TerminalTracePort =
    val forJournal: journal: AgentJournal -> TerminalTracePort
