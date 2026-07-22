module Wanxiangshu.Tests.SubagentPromptBuildTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.SubagentIntentsCodec
open Wanxiangshu.Runtime.SubagentPromptBuild
open Wanxiangshu.Runtime.WorkspaceFiles

let private coderSample: CoderIntent =
    { objective = "fix bug"
      background = "user reported failure"
      targets =
        [ { file = "a.ts"
            guide = "fix root cause"
            draft = None } ]
      doNotTouch = [||] }

let private inspectorSample: InspectorIntent =
    { objective = "find auth"
      background = "need entry points"
      questions = [| "Where is auth configured?" |]
      entries = [||] }

let promptsForParallelIntentsMatchesFormatPrompt () =
    for host in [| opencode; mimocode |] do
        let viaHelper = promptsForParallelIntents host Coder [ coderSample ]
        let viaFormat = formatPrompt host (Coder [ coderSample ])
        equal $"coder prompts match formatPrompt host={host}" viaFormat viaHelper

        let invHelper = promptsForParallelIntents host Inspector [ inspectorSample ]
        let invFormat = formatPrompt host (Inspector [ inspectorSample ])
        equal $"inspector prompts match formatPrompt host={host}" invFormat invHelper

let buildMeditatorSectionsMatchesManualZip () =
    let dummyEntry: Wanxiangshu.Kernel.Methodology.Schema.MethodologyEntry =
        { methodologyId = "test_methodology"
          shortDefinition = "test def"
          triggerWhen = "test trigger"
          noteDescription = "test note desc"
          meditatorRole = "test role"
          outputSections = [] }

    let baseText = renderMeditatorIntent dummyEntry "why?" "my background" "note detail"

    for host in [| opencode; mimocode |] do
        // Host contracts are projected inside renderMeditatorIntentWithHost before stringify.
        let withHost =
            renderMeditatorIntentWithHost dummyEntry "why?" "my background" "note detail" (Some host)

        let viaFormat = formatPrompt host (Meditator withHost) |> List.head
        equal $"meditator formatPrompt is pass-through host={host}" withHost viaFormat

        if host = mimocode then
            check "mimocode meditator includes agent_report contract" (withHost.Contains "agent_report")
        else
            check "opencode meditator keeps base objective" (withHost.Contains "why?")

        check "meditator base has objective" (baseText.Contains "why?")

let browserPromptTextMatchesFormatPrompt () =
    for host in [| opencode; mimocode |] do
        let intent = "open example.com"
        let viaText = browserPromptText host intent
        let viaFormat = formatPrompt host (Browser intent) |> List.head
        equal $"browser prompt matches formatPrompt host={host}" viaFormat viaText

let mimocodeCoderPromptsIncludeAgentReport () =
    let prompts = promptsForParallelIntents mimocode Coder [ coderSample ]
    equal "mimocode coder prompt count" 1 prompts.Length
    check "mimocode coder prompts include agent_report" (prompts.[0].Contains "agent_report")

let parallelPromptsFromIntentsInvalidReturnsParseError () =
    match parallelPromptsFromIntents opencode "coder" parseCoderIntents Coder (box "not-an-array") with
    | Ok _ -> failwith "expected ParseError"
    | Error(ParseError(ctx, _)) -> equal "parse context" "intents" ctx
    | Error other -> failwith $"unexpected error: {other}"

let run () =
    promptsForParallelIntentsMatchesFormatPrompt ()
    buildMeditatorSectionsMatchesManualZip ()
    browserPromptTextMatchesFormatPrompt ()
    mimocodeCoderPromptsIncludeAgentReport ()
    parallelPromptsFromIntentsInvalidReturnsParseError ()
