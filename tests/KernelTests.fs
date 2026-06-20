module VibeFs.Tests.KernelTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.Executor
open VibeFs.Shell.WorkspaceFiles
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Message
open VibeFs.Kernel.Dyn

let headTail' () =
    let r = headTail "hello" 2 2
    check "headTail" (r = "he...lo")

let dedup' () =
    let s = createDedupState ()
    let r1 = deduplicate s.seenContents "same string"
    let r2 = deduplicate r1.seenOutputs "same string"
    let r3 = deduplicate r1.seenOutputs "same"
    check "dedup first" (r1.output = "same string")
    check "dedup second" (r2.output = dedupMarker)
    check "dedup substring" (r3.output = dedupMarker)

let jsBoundary' () =
    check "abort message classified" (translateJsError (createObj [ "message", box "Aborted" ]) = VibeFs.Kernel.Domain.MessageAborted)
    let text = readAssistantText [| box {| ``type`` = "message"; message = box {| role = "assistant"; content = [| box {| ``type`` = "text"; text = "hello" |} |] |} |} |] None
    check "assistant text read" (text = Some "hello")

let hostKernel' () =
    let coderIntent =
        { objective = "fix bug"
          background = "user reported failure"
          targets = [ { file = "a.ts"; guide = "fix root cause"; draft = None }; { file = "b.ts"; guide = "align types"; draft = None } ]
          doNotTouch = [| "shared.ts" |] }
    let intent = formatCoderUserPrompt coderIntent
    check "coder has file" (intent.IndexOf("a.ts") >= 0)
    check "coder has objective" (intent.IndexOf("fix bug") >= 0)
    check "coder has do_not_touch" (intent.IndexOf("shared.ts") >= 0)
    let prompt = buildMeditatorPrompt [ { file = "x.fs"; content = Some "let x = 1" } ] "why?"
    check "meditator has question" (prompt.IndexOf("why?") >= 0)
    check "meditator has content" (prompt.IndexOf("let x = 1") >= 0)
    check "meditator read-only" (prompt.IndexOf("READ-ONLY") >= 0)
    let inv =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }
    let investigatorPrompt = formatInvestigatorUserPrompt inv
    check "investigator has objective" (investigatorPrompt.IndexOf("find auth") >= 0)
    check "investigator read-only" (investigatorPrompt.IndexOf("READ-ONLY") >= 0)

let wikiFetchAnswer () =
    let wikiId = match VibeFs.Kernel.Wiki.tryParseId "0a3f" with Some value -> value | None -> failwith "0a3f should parse"
    let projection =
        Map.ofList [
            wikiId, ({ id = wikiId; q = "Q"; a = "A" } : VibeFs.Kernel.Wiki.WikiEntry)
        ]
    check "wiki fetch existing" (VibeFs.Kernel.Wiki.fetchAnswer projection "0a3f" = Ok "A")
    check "wiki fetch invalid id" (VibeFs.Kernel.Wiki.fetchAnswer projection "nope" = Error "Invalid wiki id: nope")
    check "wiki fetch missing id" (VibeFs.Kernel.Wiki.fetchAnswer projection "b912" = Error "Wiki entry not found in this session snapshot: b912")

let wikiDraftArrayParsing () =
    let drafts =
        [|
            box {| q = "Q1"; a = "A1" |}
            box {| id = "0a3f"; q = "Q2"; a = "A2" |}
        |]
    match VibeFs.Kernel.Wiki.parseDraftArray (box drafts) with
    | Ok parsed ->
        check "wiki draft parse count" (parsed.Length = 2)
        check "wiki draft parse first no id" (parsed.[0].id.IsNone)
        check "wiki draft parse second id" (parsed.[1].id = Some "0a3f")
    | Error _ -> check "wiki draft parse valid ok" false

    let invalidDrafts = [| box {| a = "A1" |} |]
    check "wiki draft parse invalid error" (match VibeFs.Kernel.Wiki.parseDraftArray (box invalidDrafts) with Error _ -> true | _ -> false)

/// P0-2: every tool description and parameter doc the two adapter layers ship
/// must come from a single Kernel-level `ToolCatalog`.  Today the strings live
/// twice (Opencode/ToolSchema + Mux/SubagentTools); these tests pin the future
/// shape: one record per tool, host-agnostic, queryable by tool name.
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

    let fetchSpec = VibeFs.Kernel.ToolCatalog.specOf "fetch_wiki"
    check "fetch wiki description mentions snapshot" (fetchSpec.description.Contains "session's wiki snapshot")
    check "fetch wiki requires id" (fetchSpec.requiredFields = [ "id" ])

    let submitSpec = VibeFs.Kernel.ToolCatalog.specOf "submit_wiki"
    check "submit wiki description mentions wiki" (submitSpec.description.Contains "wiki")
    check "submit wiki requires entries" (submitSpec.requiredFields = [ "entries" ])

    let allSpecs = VibeFs.Kernel.ToolCatalog.all
    let names = allSpecs |> List.map (fun spec -> spec.name) |> Set.ofList
    check "catalog covers coder" (Set.contains "coder" names)
    check "catalog covers investigator" (Set.contains "investigator" names)
    check "catalog covers meditator" (Set.contains "meditator" names)
    check "catalog covers browser" (Set.contains "browser" names)
    check "catalog covers executor" (Set.contains "executor" names)
    check "catalog covers fetch_wiki" (Set.contains "fetch_wiki" names)
    check "catalog covers submit_wiki" (Set.contains "submit_wiki" names)

let hostToolsWikiNames () =
    let names = VibeFs.Kernel.HostTools.allToolNames VibeFs.Kernel.HostTools.opencode |> Set.ofArray
    check "host tools include fetch_wiki" (Set.contains "fetch_wiki" names)
    check "host tools include submit_wiki" (Set.contains "submit_wiki" names)

/// P0-1: a single host-aware dispatcher must produce every subagent prompt.
/// Today each host has its own `formatXxxUserPrompt` / `formatMuxXxxUserPrompt`;
/// these tests pin the future entry point: pass `Host` + `SubagentTaskKind`,
/// receive the prompts (one per parallel intent for coder/investigator).
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
        VibeFs.Kernel.Subagent.formatPrompt host (VibeFs.Kernel.Subagent.ExecutorSummary "raw shell output")
    check "executor summary prompt count is one" (execPrompts |> List.length = 1)
    check "executor summary embeds raw output" ((execPrompts |> List.head).Contains "raw shell output")

    let webPrompts =
        VibeFs.Kernel.Subagent.formatPrompt host (VibeFs.Kernel.Subagent.WebsearchSummary("ts compiler", "raw search results blob"))
    check "websearch prompt count is one" (webPrompts |> List.length = 1)
    let webBody = webPrompts |> List.head
    check "websearch prompt embeds question" (webBody.Contains "ts compiler")
    check "websearch prompt embeds raw blob" (webBody.Contains "raw search results blob")

/// joinReports must trim and `\n---\n` separate, matching today's behaviour
/// in Tools.fs/SubagentTools.fs but moved into a single Kernel helper.
let subagentJoinReports () =
    let joined = VibeFs.Kernel.Subagent.joinReports [ "first  "; "  second" ]
    check "joinReports separator" (joined.Contains "\n---\n")
    check "joinReports trims left" (joined.StartsWith "first")
    check "joinReports trims right" (joined.EndsWith "second")

/// `Dyn.deleteKey` is the primitive HookExecute leans on to clear Mimocode
/// task extras off the original args reference. Confirm it removes keys in
/// place and silently tolerates a null target.
let dynDeleteKey () =
    let target = createObj [ "keep", box "yes"; "drop", box "bye" ]
    deleteKey target "drop"
    check "deleteKey removes key" (isNullish (get target "drop"))
    check "deleteKey preserves siblings" (str target "keep" = "yes")
    deleteKey null "missing"
    check "deleteKey null is a no-op" true

/// S1 SSOT: the loop submission message must be authored once in the Kernel,
/// not copy-pasted between Mux/SubagentTools.fs and Opencode/PluginCore.fs.
/// The shared API is `Kernel.LoopMessages.buildLoopMessage` returning a single
/// canonical string; both hosts (and slash commands) must consume that exact
/// string verbatim — no re-interpretation, no per-host wording drift.
let loopMessagesShared () =
    let task = "ship S1 refactor"
    let intro = "Loop mode is active. Complete the task above, then call submit_review with:"
    let kernelMsg = VibeFs.Kernel.LoopMessages.buildLoopMessage task [ intro ]
    check "loop message embeds task" (kernelMsg.Contains task)
    check "loop message embeds intro" (kernelMsg.Contains intro)
    check "loop message mentions submit_review" (kernelMsg.Contains "submit_review")
    check "loop message lists report field" (kernelMsg.Contains "report")
    check "loop message lists affectedFiles field" (kernelMsg.Contains "affectedFiles")
    check "loop message names reviewer" (kernelMsg.Contains "reviewer")

/// S1 SSOT: the reviewer's "submit a PASS/REJECT verdict via agent_report"
/// instruction template lives once in the Kernel, not duplicated between
/// Mux/SubagentTools.fs (review verdict) and Mux/SlashCommands.fs (loop-review
/// pre-check). Pin the shared API: `Kernel.Prompts.reviewerVerdictInstructions`
/// and `Kernel.Prompts.loopReviewVerdictInstructions` — both authored once,
/// both Kernel-pure, both host-agnostic.
let reviewerVerdictPromptsShared () =
    let verdict = VibeFs.Kernel.Prompts.reviewerVerdictInstructions
    check "reviewer verdict mentions agent_report" (verdict.Contains "agent_report")
    check "reviewer verdict mentions PASS" (verdict.Contains "PASS")
    check "reviewer verdict mentions REJECT" (verdict.Contains "REJECT")
    check "reviewer verdict mentions feedback" (verdict.Contains "feedback")
    check "reviewer verdict mentions callId" (verdict.Contains "callId")

    let preReview = VibeFs.Kernel.Prompts.loopReviewVerdictInstructions
    check "loop-review verdict mentions agent_report" (preReview.Contains "agent_report")
    check "loop-review verdict mentions PASS" (preReview.Contains "PASS")
    check "loop-review verdict mentions REJECT" (preReview.Contains "REJECT")
    check "loop-review verdict mentions actionable" (preReview.Contains "actionable")

/// S1 SSOT: the user-facing review-result rendering (Accepted/Rejected/Terminated
/// → human-readable string) lives once in the Kernel. Today it lives only in
/// `Opencode/Tools.fs::formatReviewResult` and the Mux side open-codes a
/// near-identical message. Pin a single Kernel formatter both hosts call.
let reviewResultFormattingShared () =
    let accepted = VibeFs.Kernel.Prompts.formatReviewResult VibeFs.Kernel.ReviewSession.ReviewResult.Accepted
    check "accepted text mentions passed" (accepted.ToLower().Contains "passed" || accepted.ToLower().Contains "accepted")
    check "accepted text mentions loop ended" (accepted.ToLower().Contains "loop")

    let rejected = VibeFs.Kernel.Prompts.formatReviewResult (VibeFs.Kernel.ReviewSession.ReviewResult.Rejected "missing tests")
    check "rejected text embeds feedback" (rejected.Contains "missing tests")
    check "rejected text instructs to retry" (rejected.Contains "submit_review")

    let terminated = VibeFs.Kernel.Prompts.formatReviewResult VibeFs.Kernel.ReviewSession.ReviewResult.Terminated
    check "terminated text mentions terminated" (terminated.ToLower().Contains "terminat")
