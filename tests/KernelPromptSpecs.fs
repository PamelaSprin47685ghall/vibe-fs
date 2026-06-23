module VibeFs.Tests.KernelPromptSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.SearchPrompts
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.Domain
open VibeFs.Shell.Dyn


let hostKernel' () =
    let coderIntent =
        { objective = "fix bug"
          background = "user reported failure"
          targets = [ { file = "a.ts"; guide = "fix root cause"; draft = None }; { file = "b.ts"; guide = "align types"; draft = None } ]
          doNotTouch = [| "shared.ts" |] }
    let intent = coderPrompt coderIntent
    check "coder has file" (intent.IndexOf("a.ts") >= 0)
    check "coder has objective" (intent.IndexOf("fix bug") >= 0)
    check "coder has do_not_touch" (intent.IndexOf("shared.ts") >= 0)
    let prompt = meditatorPrompt [ { file = "x.fs"; content = Some "let x = 1" } ] "why?"
    check "meditator has question" (prompt.IndexOf("why?") >= 0)
    check "meditator has content" (prompt.IndexOf("let x = 1") >= 0)
    check "meditator read-only" (prompt.IndexOf("READ-ONLY") >= 0)
    let inv =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }
    let investigatorPromptText = investigatorPrompt inv
    check "investigator has objective" (investigatorPromptText.IndexOf("find auth") >= 0)
    check "investigator read-only" (investigatorPromptText.IndexOf("READ-ONLY") >= 0)

/// P0-2: every tool description and parameter doc the two adapter layers ship
/// must come from a single Kernel-level `ToolCatalog`.
let toolCatalogCentralized () =
    let coderSpec = VibeFs.Kernel.ToolCatalog.specOf "coder"
    check "coder spec carries description" (coderSpec.description.Length > 0)
    check "coder description names intents" (coderSpec.description.Contains "intents")
    check "coder requires intents+tdd" (coderSpec.requiredFields = [ "intents"; "tdd" ])
    check "coder param doc for intents" (Map.containsKey "intents" coderSpec.paramDocs)
    check "coder param doc for tdd" (Map.containsKey "tdd" coderSpec.paramDocs)

    let investigatorSpec = VibeFs.Kernel.ToolCatalog.specOf "investigator"
    check "investigator spec carries description" (investigatorSpec.description.Length > 0)
    check "investigator requires intents only" (investigatorSpec.requiredFields = [ "intents" ])

    let executorSpec = VibeFs.Kernel.ToolCatalog.specOf "executor"
    check "executor describes timeout budgets" (executorSpec.description.Contains "timeout")
    check "executor requires mode" (executorSpec.requiredFields |> List.contains "mode")
    check "executor param doc for mode" (Map.containsKey "mode" executorSpec.paramDocs)

    let fetchSpec = VibeFs.Kernel.ToolCatalog.specOf "knowledge_graph_fetch"
    check "fetch knowledge graph description mentions snapshot" (fetchSpec.description.Contains "session's knowledge graph snapshot")
    check "fetch knowledge graph requires entity" (fetchSpec.requiredFields = [ "entity" ])

    let submitSpec = VibeFs.Kernel.ToolCatalog.specOf "return_bookkeeper"
    check "return bookkeeper description mentions knowledge graph" (submitSpec.description.Contains "knowledge graph")
    check "return bookkeeper requires entries" (submitSpec.requiredFields = [ "entries" ])

    let allSpecs = VibeFs.Kernel.ToolCatalog.all
    let names = allSpecs |> List.map (fun spec -> spec.name) |> Set.ofList
    check "catalog covers coder" (Set.contains "coder" names)
    check "catalog covers investigator" (Set.contains "investigator" names)
    check "catalog covers meditator" (Set.contains "meditator" names)
    check "catalog covers browser" (Set.contains "browser" names)
    check "catalog covers executor" (Set.contains "executor" names)
    check "catalog covers knowledge_graph_fetch" (Set.contains "knowledge_graph_fetch" names)
    check "catalog covers return_bookkeeper" (Set.contains "return_bookkeeper" names)

let hostToolsKnowledgeGraphNames () =
    let names = VibeFs.Kernel.HostTools.allToolNames VibeFs.Kernel.HostTools.opencode |> Set.ofArray
    check "host tools include knowledge_graph_fetch" (Set.contains "knowledge_graph_fetch" names)
    check "host tools include return_bookkeeper" (Set.contains "return_bookkeeper" names)

/// P0-1: a single host-aware dispatcher must produce every subagent prompt.
let subagentDispatch () =
    let host = VibeFs.Kernel.HostTools.opencode
    let muxHost = VibeFs.Kernel.HostTools.mimocode

    let coderIntent : CoderIntent =
        { objective = "fix bug"
          background = "user reported failure"
          targets = [ { file = "a.ts"; guide = "fix root cause"; draft = None } ]
          doNotTouch = [||] }

    let opencodeCoderPrompts =
        VibeFs.Kernel.Subagent.formatPrompt host (VibeFs.Kernel.Subagent.Coder [ coderIntent ])
    check "opencode coder prompt count" (opencodeCoderPrompts |> List.length = 1)
    let opencodeBody = opencodeCoderPrompts |> List.head
    check "opencode coder mentions objective" (opencodeBody.Contains "fix bug")
    check "opencode coder has no agent_report tail" (not (opencodeBody.Contains "agent_report"))

    let muxCoderPrompts =
        VibeFs.Kernel.Subagent.formatPrompt muxHost (VibeFs.Kernel.Subagent.Coder [ coderIntent ])
    let muxBody = muxCoderPrompts |> List.head
    check "mux coder mentions objective" (muxBody.Contains "fix bug")
    check "mux coder ends in agent_report tail" (muxBody.Contains "agent_report")

    let investigatorIntent : InvestigatorIntent =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }
    let invPrompts =
        VibeFs.Kernel.Subagent.formatPrompt host (VibeFs.Kernel.Subagent.Investigator [ investigatorIntent ])
    check "investigator prompt count" (invPrompts |> List.length = 1)
    check "investigator prompt mentions objective" ((invPrompts |> List.head).Contains "find auth")

    let browserPrompts =
        VibeFs.Kernel.Subagent.formatPrompt host (VibeFs.Kernel.Subagent.Browser "open google.com")
    check "browser prompt count is one" (browserPrompts |> List.length = 1)
    check "browser prompt embeds intent" ((browserPrompts |> List.head).Contains "open google.com")

    let execPrompts =
        VibeFs.Kernel.Subagent.formatPrompt host (VibeFs.Kernel.Subagent.ExecutorSummary("raw shell output", "shell", "echo 1", [ "dep1" ], "short", "ro"))
    check "executor summary prompt count is one" (execPrompts |> List.length = 1)
    let execPrompt = execPrompts |> List.head
    check "executor summary embeds language" (execPrompt.Contains "language: \"shell\"")
    check "executor summary embeds program" (execPrompt.Contains "program: |\n  echo 1")
    check "executor summary embeds dependencies" (execPrompt.Contains "dependencies:\n  - \"dep1\"")
    check "executor summary embeds timeout_type" (execPrompt.Contains "timeout_type: \"short\"")
    check "executor summary embeds mode" (execPrompt.Contains "mode: \"ro\"")
    check "executor summary embeds raw output" (execPrompt.Contains "raw_output: |\n  raw shell output")

    let webPrompts =
        VibeFs.Kernel.Subagent.formatPrompt host (VibeFs.Kernel.Subagent.WebsearchSummary("ts compiler", "raw search results blob"))
    check "websearch prompt count is one" (webPrompts |> List.length = 1)
    let webBody = webPrompts |> List.head
    check "websearch prompt embeds question" (webBody.Contains "ts compiler")
    check "websearch prompt embeds raw blob" (webBody.Contains "raw search results blob")

/// joinReports must trim and `\n---\n` separate.
let subagentJoinReports () =
    let joined = VibeFs.Kernel.Subagent.joinReports [ "first  "; "  second" ]
    check "joinReports separator" (joined.Contains "\n---\n")
    check "joinReports trims left" (joined.StartsWith "first")
    check "joinReports trims right" (joined.EndsWith "second")

/// S1 SSOT: the loop submission message must be authored once in the Kernel.
let loopMessagesShared () =
    let task = "ship S1 refactor"
    let intro = "With-Review Mode is active. Complete the task above, then call submit_review with:"
    let kernelMsg = VibeFs.Kernel.LoopMessages.buildLoopMessage task [ intro ]
    check "loop message carries task as block field" (kernelMsg.Contains "task: |")
    check "loop message embeds task" (kernelMsg.Contains task)
    check "loop message embeds intro" (kernelMsg.Contains intro)
    check "loop message mentions submit_review" (kernelMsg.Contains "submit_review")
    check "loop message lists report field" (kernelMsg.Contains "report")
    check "loop message lists affectedFiles field" (kernelMsg.Contains "affectedFiles")
    check "loop message names reviewer" (kernelMsg.Contains "reviewer")

    let multilineTask = "ship S1 refactor\ninclude follow-up cleanup"
    let parsedLoopMsg = VibeFs.Kernel.PromptFrontMatter.parseFrontMatterScalars (VibeFs.Kernel.LoopMessages.buildLoopMessage multilineTask [ intro ])
    equal "loop message multiline task round-trips through block front-matter" (Some multilineTask) (Map.tryFind "task" parsedLoopMsg)

    let loopTemplate = VibeFs.Kernel.ReviewPrompts.withReviewCommandTemplate
    check "loop template carries command front-matter" (loopTemplate.Contains "command: \"with-review\"")
    check "loop template carries task front-matter block placeholder" (loopTemplate.Contains "task: |\n  $ARGUMENTS")
    check "loop template does not say task is repeated below" (not (loopTemplate.Contains "repeated below"))
    check "loop template reuses review criteria" (loopTemplate.Contains "# Evaluation Criteria")
    check "loop template mentions submit_review" (loopTemplate.Contains "submit_review")
    check "loop template forbids finishing early" (loopTemplate.Contains "Do not end the conversation")

    let precheckTemplate = VibeFs.Kernel.ReviewPrompts.withReviewPrecheckCommandTemplate
    check "precheck template carries command front-matter" (precheckTemplate.Contains "command: \"with-review-precheck\"")
    check "precheck template carries task front-matter block placeholder" (precheckTemplate.Contains "task: |\n  $ARGUMENTS")
    check "precheck template reuses review criteria" (precheckTemplate.Contains "# Evaluation Criteria")
    check "precheck template does not repeat task in body tail" (not (precheckTemplate.EndsWith "$ARGUMENTS"))

/// S1 SSOT: the reviewer's verdict instruction template lives once in the Kernel.
let reviewerVerdictPromptsShared () =
    let verdict = VibeFs.Kernel.ReviewPrompts.ReviewerVerdictPrompts.reviewerVerdictInstructions
    check "reviewer verdict mentions agent_report" (verdict.Contains "agent_report")
    check "reviewer verdict mentions PASS" (verdict.Contains "PASS")
    check "reviewer verdict mentions REJECT" (verdict.Contains "REJECT")
    check "reviewer verdict mentions feedback" (verdict.Contains "feedback")

    let preReview = VibeFs.Kernel.ReviewPrompts.ReviewerVerdictPrompts.loopReviewVerdictInstructions
    check "loop-review verdict mentions agent_report" (preReview.Contains "agent_report")
    check "loop-review verdict mentions PASS" (preReview.Contains "PASS")
    check "loop-review verdict mentions REJECT" (preReview.Contains "REJECT")
    check "loop-review verdict mentions actionable" (preReview.Contains "actionable")

/// S1 SSOT: the user-facing review-result rendering lives once in the Kernel.
let reviewResultFormattingShared () =
    let accepted = VibeFs.Kernel.ReviewPrompts.formatReviewResult VibeFs.Kernel.ReviewSession.ReviewResult.Accepted
    check "accepted text mentions passed" (accepted.ToLower().Contains "passed" || accepted.ToLower().Contains "accepted")
    check "accepted text signals with-review ended" (accepted.ToLower().Contains "with-review")

    let rejected = VibeFs.Kernel.ReviewPrompts.formatReviewResult (VibeFs.Kernel.ReviewSession.ReviewResult.Rejected "missing tests")
    check "rejected text embeds feedback" (rejected.Contains "missing tests")
    check "rejected text instructs to retry" (rejected.Contains "submit_review")

    let terminated = VibeFs.Kernel.ReviewPrompts.formatReviewResult VibeFs.Kernel.ReviewSession.ReviewResult.Terminated
    check "terminated text mentions terminated" (terminated.ToLower().Contains "terminat")

/// S2 red: pin the extended DomainError union before the Kernel rewrite lands.
let domainErrorsShared () =
    let err1 = VibeFs.Kernel.Domain.DomainError.ExecutorExecutableMissing "npm"
    let err2 = VibeFs.Kernel.Domain.DomainError.ParseError("json", "missing bracket")
    let err3 = VibeFs.Kernel.Domain.DomainError.ToolNotPermitted("coder", "bash")
    let err4 = VibeFs.Kernel.Domain.DomainError.InvalidIntent("coder", "tdd", "unknown phase")
    let err5 = VibeFs.Kernel.Domain.DomainError.UpstreamTimeout 30
    let err6 = VibeFs.Kernel.Domain.DomainError.UpstreamRefused "rate limit"
    check "err1 is executable missing" (match err1 with VibeFs.Kernel.Domain.DomainError.ExecutorExecutableMissing "npm" -> true | _ -> false)
    check "err2 is parse error" (match err2 with VibeFs.Kernel.Domain.DomainError.ParseError("json", "missing bracket") -> true | _ -> false)
    check "err3 is tool not permitted" (match err3 with VibeFs.Kernel.Domain.DomainError.ToolNotPermitted("coder", "bash") -> true | _ -> false)
    check "err4 is invalid intent" (match err4 with VibeFs.Kernel.Domain.DomainError.InvalidIntent("coder", "tdd", "unknown phase") -> true | _ -> false)
    check "err5 is upstream timeout" (match err5 with VibeFs.Kernel.Domain.DomainError.UpstreamTimeout 30 -> true | _ -> false)
    check "err6 is upstream refused" (match err6 with VibeFs.Kernel.Domain.DomainError.UpstreamRefused "rate limit" -> true | _ -> false)
