module Wanxiangshu.Tests.SubagentPromptBuildTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.SubagentIntentsCodec
open Wanxiangshu.Shell.SubagentPromptBuild
open Wanxiangshu.Shell.WorkspaceFiles

let private coderSample: CoderIntent =
    { objective = "fix bug"
      background = "user reported failure"
      targets =
        [ { file = "a.ts"
            guide = "fix root cause"
            draft = None } ]
      doNotTouch = [||] }

let private investigatorSample: InvestigatorIntent =
    { objective = "find auth"
      background = "need entry points"
      questions = [| "Where is auth configured?" |]
      entries = [||] }

let promptsForParallelIntentsMatchesFormatPrompt () =
    for host in [| opencode; mimocode |] do
        let viaHelper = promptsForParallelIntents host Coder [ coderSample ]
        let viaFormat = formatPrompt host (Coder [ coderSample ])
        equal $"coder prompts match formatPrompt host={host}" viaFormat viaHelper

        let invHelper = promptsForParallelIntents host Investigator [ investigatorSample ]
        let invFormat = formatPrompt host (Investigator [ investigatorSample ])
        equal $"investigator prompts match formatPrompt host={host}" invFormat invHelper

let buildMeditatorSectionsMatchesManualZip () =
    let dummyEntry: Wanxiangshu.Methodology.SchemaCommon.MethodologyEntry =
        { methodologyId = "test_methodology"
          shortDefinition = "test def"
          triggerWhen = "test trigger"
          noteDescription = "test note desc"
          meditatorRole = "test role"
          outputSections = [] }

    let viaText =
        Wanxiangshu.Methodology.SchemaCommon.renderMeditatorIntent dummyEntry "why?" "my background" "note detail"

    for host in [| opencode; mimocode |] do
        let viaFormat = formatPrompt host (Meditator viaText) |> List.head

        let expected =
            if host = mimocode then
                viaText
                + "\n\nWhen you have finished the task, you MUST call the agent_report tool. Use structuredOutput with relatedFiles (and relatedCode where applicable) so the caller can act on your findings."
            else
                viaText

        equal $"meditator prompt matches formatPrompt host={host}" expected viaFormat

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
