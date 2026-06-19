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
    let r1 = deduplicate s.seenContents "same"
    let r2 = deduplicate r1.seenOutputs "same"
    check "dedup first" (r1.output = "same")
    check "dedup second" (r2.output = dedupMarker)

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

    let allSpecs = VibeFs.Kernel.ToolCatalog.all
    let names = allSpecs |> List.map (fun spec -> spec.name) |> Set.ofList
    check "catalog covers coder" (Set.contains "coder" names)
    check "catalog covers investigator" (Set.contains "investigator" names)
    check "catalog covers meditator" (Set.contains "meditator" names)
    check "catalog covers browser" (Set.contains "browser" names)
    check "catalog covers executor" (Set.contains "executor" names)

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

/// P2-1: hook arg rewrites become pure data (`ArgsPatch`) plus one boundary
/// applier.  These tests pin the in-place mutation contract: setKeys must land
/// before deleteKeys is honoured, both must mutate the SAME reference (the
/// host re-parses it), and a null target is a silent no-op.
let argsPatchAppliesInPlace () =
    let target = createObj [ "keep", box "yes"; "drop", box "bye"; "old", box 1 ]
    let alias = target
    let patch = { setKeys = [ "old", box 42; "fresh", box "new" ]; deleteKeys = [ "drop" ] }
    applyPatch target patch
    check "applyPatch mutates same reference" (obj.ReferenceEquals(target, alias))
    check "applyPatch keeps untouched key" (str alias "keep" = "yes")
    check "applyPatch overwrites set key" (unbox<int> (get alias "old") = 42)
    check "applyPatch installs new set key" (str alias "fresh" = "new")
    check "applyPatch deletes drop key" (isNullish (get alias "drop"))

let argsPatchSkipsNullTarget () =
    // A null target must be tolerated: nested objects (e.g. an absent
    // `operation`) frequently show up as null and must not throw.
    applyPatch null { setKeys = [ "x", box 1 ]; deleteKeys = [ "y" ] }
    check "applyPatch null target is a no-op" true

let argsPatchEmptyIsNoop () =
    let target = createObj [ "x", box 1 ]
    applyPatch target emptyPatch
    check "emptyPatch leaves keys alone" (unbox<int> (get target "x") = 1)
