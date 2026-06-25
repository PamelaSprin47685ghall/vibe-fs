module VibeFs.Tests.SubagentPromptBuildTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.Domain
open VibeFs.Shell.SubagentIntentsCodec
open VibeFs.Shell.SubagentPromptBuild
open VibeFs.Shell.WorkspaceFiles

let private coderSample : CoderIntent =
    { objective = "fix bug"
      background = "user reported failure"
      targets = [ { file = "a.ts"; guide = "fix root cause"; draft = None } ]
      doNotTouch = [||] }

let private investigatorSample : InvestigatorIntent =
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
    let files = [| "x.fs"; "y.fs" |]
    let results =
        [| { ReverieFileResult.filePath = "x.fs"; content = Some "let x = 1"; skipReason = None }
           { ReverieFileResult.filePath = "y.fs"; content = None; skipReason = None } |]
    let built = buildMeditatorSections files results
    equal "section count" 2 built.Length
    equal "first file" "x.fs" built.[0].file
    equal "first content" (Some "let x = 1") built.[0].content
    equal "second content" None built.[1].content

    for host in [| opencode; mimocode |] do
        let sections = built |> Array.toList
        let viaText = meditatorPromptText host "why?" sections
        let viaFormat = formatPrompt host (Meditator("why?", sections)) |> List.head
        equal $"meditator prompt matches formatPrompt host={host}" viaFormat viaText

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
    | Error (ParseError (ctx, _)) -> equal "parse context" "intents" ctx
    | Error other -> failwith $"unexpected error: {other}"

let run () =
    promptsForParallelIntentsMatchesFormatPrompt ()
    buildMeditatorSectionsMatchesManualZip ()
    browserPromptTextMatchesFormatPrompt ()
    mimocodeCoderPromptsIncludeAgentReport ()
    parallelPromptsFromIntentsInvalidReturnsParseError ()