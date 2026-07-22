module Wanxiangshu.Hosts.Omp.ExecutorSummarizer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Runtime.OmpHostBindings

module Dyn = Wanxiangshu.Runtime.Dyn

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let private byteLength (s: string) : int = nodeBuffer?byteLength (s, "utf-8")
let private truncateToBytes (s: string) (n: int) : string = truncateUtf8ByBytes s n

/// Summarize executor output on a child session. Anchors to target turn via
/// entry baseline — never treats an arbitrary pre-existing idle as completion.
/// Evidence is built from the real ExecuteResult (status/exit/signal/truncation).
let summarizeOutput
    (childSession: obj)
    (result: ExecuteResult)
    (lang: string)
    (command: string)
    (deps: string list)
    (what: string)
    =
    promise {
        let evidence = buildExecutorEvidence byteLength truncateToBytes result

        let summaryPrompt =
            executorSummarizerPrompt what evidence lang command deps TimeoutKind.Long

        let baseline = entryCountOfSession childSession
        let! _ = sessionPrompt childSession summaryPrompt
        let! grew = waitForIdleAfterBaseline childSession baseline 8
        let sm = unbox<ISessionManager> (Dyn.get childSession "sessionManager")
        let fallback = outputFromResult result

        let text =
            if grew then
                match readAssistantText sm baseline "\n\n" with
                | Some t -> t
                | None -> fallback
            else
                fallback

        return textResult text
    }
