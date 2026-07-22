module Wanxiangshu.Tests.KernelPromptSpecsSubagent

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts

[<Import("parse", "smol-toml")>]
let private parseToml (text: string) : obj = jsNative

let private strField (o: obj) (k: string) : string =
    let v = o?(k)
    if isNull v then "" else string v

let private hasRuleKind (doc: obj) (kind: string) : bool =
    let rules = doc?rules

    if isNull rules then
        false
    else
        unbox<obj array> rules |> Array.exists (fun r -> strField r "kind" = kind)

let private hasTargetKind (doc: obj) (kind: string) : bool =
    let targets = doc?targets

    if isNull targets then
        false
    else
        unbox<obj array> targets |> Array.exists (fun t -> strField t "kind" = kind)

let private sampleCoderIntent: CoderIntent =
    { objective = "fix bug"
      background = "user reported failure"
      targets =
        [ { file = "a.ts"
            guide = "fix root cause"
            draft = None } ]
      doNotTouch = [||] }

let private assertCoderPrompts () =
    let opencodePrompt = formatPrompt opencode (Coder [ sampleCoderIntent ]) |> List.head
    let opencodeDoc = parseToml opencodePrompt
    equal "opencode coder objective field" "fix bug" (strField opencodeDoc "objective")
    check "opencode coder has file target" (hasTargetKind opencodeDoc "file")
    check "opencode coder has no agent_report contract" (not (opencodePrompt.Contains "agent_report"))

    let muxPrompt = formatPrompt mimocode (Coder [ sampleCoderIntent ]) |> List.head
    let muxDoc = parseToml muxPrompt
    equal "mux coder objective field" "fix bug" (strField muxDoc "objective")
    check "mux coder ends in agent_report contract" (muxPrompt.Contains "agent_report")

let private assertInspectorAndBrowserPrompts () =
    let inspectorIntent: InspectorIntent =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }

    let invPrompt = formatPrompt opencode (Inspector [ inspectorIntent ]) |> List.head
    let invDoc = parseToml invPrompt
    equal "inspector objective field" "find auth" (strField invDoc "objective")
    check "inspector has question rule" (hasRuleKind invDoc "question")

    let browserPromptText = formatPrompt opencode (Browser "open google.com") |> List.head
    let browserDoc = parseToml browserPromptText
    equal "browser objective field" "open google.com" (strField browserDoc "objective")

let private assertExecutorPrompts () =
    let evidence: Wanxiangshu.Kernel.Prompt.ExecutorOutputEvidence =
        { stdout = "raw shell output"
          stderr = Some "boom"
          exitStatus = "exit_error"
          exitCode = Some 2
          signal = None
          truncated = false }

    let execPrompt =
        formatPrompt
            opencode
            (ExecutorSummary(evidence, "shell", "echo 1", [ "dep1" ], Wanxiangshu.Kernel.Prompt.TimeoutKind.Short, ""))
        |> List.head

    let execDoc = parseToml execPrompt
    check "executor summary has command target" (hasTargetKind execDoc "command")
    check "executor summary has executor_output target" (hasTargetKind execDoc "executor_output")
    check "executor summary embeds program value" (execPrompt.Contains "echo 1")
    check "executor summary embeds dependency" (execPrompt.Contains "dep1")
    check "executor summary embeds short timeout" (execPrompt.Contains "short")
    check "executor summary embeds raw output evidence" (execPrompt.Contains "raw shell output")
    check "executor summary real exit_status" (execPrompt.Contains "exit_error")
    check "executor summary real exit_code" (execPrompt.Contains "2")
    check "executor summary stderr" (execPrompt.Contains "boom")
    check "executor summary no evidence bag" (not (hasTargetKind execDoc "evidence"))

    let focusEvidence =
        { evidence with
            stdout = "out"
            exitStatus = "completed"
            exitCode = Some 0
            stderr = None }

    let focusDoc =
        formatPrompt
            opencode
            (ExecutorSummary(focusEvidence, "shell", "echo 1", [], Wanxiangshu.Kernel.Prompt.TimeoutKind.Short, "only exit codes"))
        |> List.head
        |> parseToml

    equal "executor summary objective is whatToSummarize" "only exit codes" (strField focusDoc "objective")

let private assertWebsearchPrompts () =
    let webResults: Wanxiangshu.Kernel.Prompt.WebSearchResultItem list =
        [ { title = "TS handbook"
            url = "https://www.typescriptlang.org/docs"
            content = "raw search results blob" } ]

    let webPrompt = formatPrompt opencode (WebsearchSummary("ts compiler", webResults)) |> List.head
    let webDoc = parseToml webPrompt
    equal "websearch objective field" "ts compiler" (strField webDoc "objective")
    check "websearch has websearch_results target" (hasTargetKind webDoc "websearch_results")
    check "websearch embeds raw blob evidence" (webPrompt.Contains "raw search results blob")
    check "websearch embeds real url" (webPrompt.Contains "https://www.typescriptlang.org/docs")
    check "websearch no evidence bag" (not (hasTargetKind webDoc "evidence"))
    check "websearch no raw_results bag title" (not (webPrompt.Contains "raw_results"))

let subagentDispatch () =
    assertCoderPrompts ()
    assertInspectorAndBrowserPrompts ()
    assertExecutorPrompts ()
    assertWebsearchPrompts ()

let subagentJoinReports () =
    let joined =
        joinReports
            [ reportFromText "first  "
              { reportFromText "  second" with
                  findings = [ "finding-a" ]
                  relatedFiles = [ "src/A.fs" ]
                  relatedCode = [ "let x = 1" ] } ]

    let doc = parseToml joined
    check "joinReports parses as table" (not (isNull doc))
    check "joinReports uses reports table" (joined.Contains "reports" || joined.Contains "[[reports]]")
    check "joinReports embeds first summary" (joined.Contains "first")
    check "joinReports embeds second summary" (joined.Contains "second")
    check "joinReports embeds findings" (joined.Contains "finding-a")
    check "joinReports embeds related_files" (joined.Contains "src/A.fs")
    check "joinReports embeds related_code" (joined.Contains "let x = 1")
    check "joinReports no markdown divider" (not (joined.Contains "\n---\n"))

let mimocodeFormatPromptAppendsAgentReportTail () =
    let mimocodePrompt = formatPrompt Mimocode (Browser "open google.com") |> List.head
    let mimocodeDoc = parseToml mimocodePrompt
    equal "mimocode browser objective" "open google.com" (strField mimocodeDoc "objective")
    check "mimocode browser has agent_report contract" (mimocodePrompt.Contains "agent_report")

    let opencodePrompt = formatPrompt Opencode (Browser "open google.com") |> List.head
    check "opencode browser has no agent_report contract" (not (opencodePrompt.Contains "agent_report"))

let meditatorMentionsReadCapability () =
    let dummyEntry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry =
        { methodologyId = "test_methodology"
          shortDefinition = "test def"
          triggerWhen = "test trigger"
          noteDescription = "test note desc"
          meditatorRole = "test role"
          outputSections = [] }

    let prompt =
        renderMeditatorIntent dummyEntry "what is the core abstraction?" "my background" "note detail"

    let doc = parseToml prompt
    equal "meditator objective" "what is the core abstraction?" (strField doc "objective")
    check "meditator prompt is no-tools" (prompt.ToLowerInvariant().Contains "do not call tools")
    check "meditator methodology structured id" (prompt.Contains "methodology_id" || prompt.Contains "test_methodology")
    check "meditator no METHODOLOGY_ID prose" (not (prompt.Contains "METHODOLOGY_ID:"))
