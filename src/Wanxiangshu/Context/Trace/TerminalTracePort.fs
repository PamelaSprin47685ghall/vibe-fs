namespace Wanxiangshu.Context.Trace

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

type TerminalTracePort =
    { CaptureTerminalText:
        SessionId -> string -> ProviderRunIdentity -> Task<Result<XTraceCaptureReceipt, XTraceCaptureError>> }

[<RequireQualifiedAccess>]
module TerminalTracePort =
    let forJournal (journal: AgentJournal) : TerminalTracePort =
        { CaptureTerminalText =
            fun sessionId text providerRun ->
                XTraceCapture.captureTerminalTextWithReceipt (Some journal) sessionId text providerRun }
