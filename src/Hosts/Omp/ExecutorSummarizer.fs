module Wanxiangshu.Hosts.Omp.ExecutorSummarizer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Runtime.OmpHostBindings

module Dyn = Wanxiangshu.Runtime.Dyn

/// Summarize executor output on a child session. Anchors to target turn via
/// entry baseline — never treats an arbitrary pre-existing idle as completion.
let summarizeOutput
    (childSession: obj)
    (output: string)
    (lang: string)
    (command: string)
    (deps: string list)
    (mode: string)
    (what: string)
    =
    promise {
        let summaryPrompt =
            executorSummarizerPrompt what output lang command deps "executor" mode

        let baseline = entryCountOfSession childSession
        let! _ = sessionPrompt childSession summaryPrompt
        let! grew = waitForIdleAfterBaseline childSession baseline 8
        let sm = unbox<ISessionManager> (Dyn.get childSession "sessionManager")

        let text =
            if grew then
                match readAssistantText sm baseline "\n\n" with
                | Some t -> t
                | None -> output
            else
                output

        return textResult text
    }
