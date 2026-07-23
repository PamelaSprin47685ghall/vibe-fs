module Wanxiangshu.Runtime.SubagentSummarizerPrompts

open System
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt

let private hostRules (host: Host option) : PromptRule list =
    match host with
    | Some Host.Mimocode ->
        [ PromptRule.Contract
              "When you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable) so the caller can act on your findings." ]
    | _ -> []

let private renderOrFail (docView: PromptDocumentView) (label: string) : string =
    match PromptDocument.create docView with
    | Ok doc -> PromptToml.render doc
    | Error errs -> failwithf "Failed to create %s PromptDocument: %A" label errs

/// Summarizer prompt from already-structured executor evidence (no second truncation).
let executorSummarizerPromptWithHost
    (whatToSummarize: string)
    (evidence: ExecutorOutputEvidence)
    (language: string)
    (program: string)
    (dependencies: string list)
    (timeoutKind: TimeoutKind)
    (host: Host option)
    : string =
    let objective =
        if String.IsNullOrWhiteSpace whatToSummarize then
            "Summarize the executor output while preserving stack traces"
        else
            whatToSummarize.Trim()

    let docView: PromptDocumentView =
        { objective = objective
          background = None
          agentRole = AgentRole.ExecutorSummarization
          targets =
            [ PromptTarget.CommandTarget(language, program, dependencies, timeoutKind)
              PromptTarget.ExecutorOutputTarget evidence ]
          boundaries = []
          rules =
            [ PromptRule.Constraint
                  "Preserve errors, stack traces, and key paths or values. Omit noise, repeated lines, and progress banners. Do not invent details that are not in the output. Do NOT lose any information."
              yield! hostRules host ]
          outcomes =
            [ { label = "report"
                text = "Return a dense summary without inventing facts." } ] }

    renderOrFail docView "ExecutorSummarizer"

let executorSummarizerPrompt
    (whatToSummarize: string)
    (evidence: ExecutorOutputEvidence)
    (language: string)
    (program: string)
    (dependencies: string list)
    (timeoutKind: TimeoutKind)
    : string =
    executorSummarizerPromptWithHost whatToSummarize evidence language program dependencies timeoutKind None


let renderMeditatorIntentWithHost
    (entry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry)
    (intentText: string)
    (backgroundText: string)
    (noteText: string)
    (host: Host option)
    : string =
    let baseView =
        Wanxiangshu.Kernel.Methodology.Schema.renderMeditatorDocument entry intentText backgroundText noteText

    let docView =
        { baseView with
            rules = baseView.rules @ hostRules host }

    renderOrFail docView "Meditator"

let renderMeditatorIntent
    (entry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry)
    (intentText: string)
    (backgroundText: string)
    (noteText: string)
    : string =
    renderMeditatorIntentWithHost entry intentText backgroundText noteText None
