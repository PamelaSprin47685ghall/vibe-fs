module Wanxiangshu.Tests.KernelPromptSpecsSubagent

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Runtime.SubagentPrompts

let subagentDispatch () =
    let host = opencode
    let muxHost = mimocode

    let coderIntent: CoderIntent =
        { objective = "fix bug"
          background = "user reported failure"
          targets =
            [ { file = "a.ts"
                guide = "fix root cause"
                draft = None } ]
          doNotTouch = [||] }

    let opencodeCoderPrompts = formatPrompt host (Coder [ coderIntent ])
    check "opencode coder prompt count" (opencodeCoderPrompts |> List.length = 1)
    let opencodeBody = opencodeCoderPrompts |> List.head
    check "opencode coder mentions objective" (opencodeBody.Contains "fix bug")
    check "opencode coder has no agent_report tail" (not (opencodeBody.Contains "agent_report"))

    let muxCoderPrompts = formatPrompt muxHost (Coder [ coderIntent ])
    let muxBody = muxCoderPrompts |> List.head
    check "mux coder mentions objective" (muxBody.Contains "fix bug")
    check "mux coder ends in agent_report tail" (muxBody.Contains "agent_report")

    let inspectorIntent: InspectorIntent =
        { objective = "find auth"
          background = "need entry points"
          questions = [| "Where is auth configured?" |]
          entries = [||] }

    let invPrompts = formatPrompt host (Inspector [ inspectorIntent ])
    check "inspector prompt count" (invPrompts |> List.length = 1)
    check "inspector prompt mentions objective" ((invPrompts |> List.head).Contains "find auth")

    let browserPrompts = formatPrompt host (Browser "open google.com")
    check "browser prompt count is one" (browserPrompts |> List.length = 1)
    check "browser prompt embeds intent" ((browserPrompts |> List.head).Contains "open google.com")

    let execPrompts =
        formatPrompt host (ExecutorSummary("raw shell output", "shell", "echo 1", [ "dep1" ], "short", ""))

    check "executor summary prompt count is one" (execPrompts |> List.length = 1)
    let execPrompt = execPrompts |> List.head
    check "executor summary embeds language" (execPrompt.Contains "shell")
    check "executor summary embeds program" (execPrompt.Contains "program" && execPrompt.Contains "echo 1")
    check "executor summary embeds dependencies" (execPrompt.Contains "dep1")
    check "executor summary embeds timeout_type" (execPrompt.Contains "short")
    check "executor summary embeds raw output in body" (execPrompt.Contains "raw shell output")

    let execPromptsWithFocus =
        formatPrompt host (ExecutorSummary("out", "shell", "echo 1", [], "short", "only exit codes"))

    check "executor summary embeds whatToSummarize" ((execPromptsWithFocus |> List.head).Contains "only exit codes")

    let webPrompts =
        formatPrompt host (WebsearchSummary("ts compiler", "raw search results blob"))

    check "websearch prompt count is one" (webPrompts |> List.length = 1)
    let webBody = webPrompts |> List.head
    check "websearch prompt embeds question" (webBody.Contains "ts compiler")
    check "websearch prompt embeds raw blob" (webBody.Contains "raw search results blob")

let subagentJoinReports () =
    let joined = joinReports [ "first  "; "  second" ]
    check "joinReports separator" (joined.Contains "\n---\n")
    check "joinReports trims left" (joined.StartsWith "first")
    check "joinReports trims right" (joined.EndsWith "second")

let mimocodeFormatPromptAppendsAgentReportTail () =
    let browserPrompts = formatPrompt Mimocode (Browser "open google.com")
    let mimocodeBody = browserPrompts |> List.head

    check
        "mimocode browser prompt contains MUST call the agent_report"
        (mimocodeBody.Contains "MUST call the agent_report")

    let opencodePrompts = formatPrompt Opencode (Browser "open google.com")
    let opencodeBody = opencodePrompts |> List.head

    check
        "opencode browser prompt does not append agent_report tail"
        (not (opencodeBody.Contains "MUST call the agent_report"))

let meditatorMentionsReadCapability () =
    let dummyEntry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry =
        { methodologyId = "test_methodology"
          shortDefinition = "test def"
          triggerWhen = "test trigger"
          noteDescription = "test note desc"
          meditatorRole = "test role"
          outputSections = [] }

    let body =
        Wanxiangshu.Kernel.Methodology.Schema.renderMeditatorIntent
            dummyEntry
            "what is the core abstraction?"
            "my background"
            "note detail"

    check "meditator prompt mentions read tool" (body.Contains "read tool")
    check "meditator prompt still embeds question" (body.Contains "what is the core abstraction?")
