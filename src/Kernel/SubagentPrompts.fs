module VibeFs.Kernel.SubagentPrompts

open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.PromptFrontMatter
open VibeFs.Kernel.PromptFragments

let meditatorSkippedSection = "(skipped)"

type MeditatorFileSection =
    { file: string; content: string option }

let private coderTargetItem (t: CoderTarget) : string =
    let guideLines = t.guide.Split('\n') |> Array.map (fun line -> "      " + line)

    let draftLines =
        match t.draft with
        | Some draft when not (System.String.IsNullOrWhiteSpace draft) ->
            Array.append [| "    draft: |" |] (draft.Split('\n') |> Array.map (fun line -> "      " + line))
        | _ -> [||]

    Array.concat
        [ [| "  - file: " + yamlStringValue t.file; "    guide: |" |]
          guideLines
          draftLines ]
    |> String.concat "\n"

let private agentPrompt fields lines =
    let actualLines =
        if
            lines
            |> List.exists (fun (l: string) -> l.StartsWith("You are an implementation agent"))
        then
            lines
        else
            readOnlyRules :: lines

    frontMatterPrompt fields (String.concat "\n\n" actualLines)

let coderPrompt (intent: CoderIntent) : string =
    let fields =
        [ yamlField "objective" intent.objective
          yamlField "background" intent.background
          yamlSeqField "targets" (intent.targets |> List.map coderTargetItem) ]
        @ (if intent.doNotTouch.Length = 0 then
               []
           else
               [ yamlStringSeqField "do_not_touch" (List.ofArray intent.doNotTouch) ])

    agentPrompt
        fields
        [ "You are an implementation agent. Read the listed files and related code, then edit or create files to satisfy the objective and each target guide."
          "Static verification only (read and think using logic). Do NOT run tests or execute code."
          "Return a detailed summary of changes and/or your difficulties." ]

let investigatorPrompt (intent: InvestigatorIntent) : string =
    agentPrompt
        [ yamlField "objective" intent.objective
          yamlField "background" intent.background
          yamlStringSeqField "questions" (List.ofArray intent.questions)
          yamlStringSeqField "entries" (List.ofArray intent.entries) ]
        [ "You are a codebase search agent. Explore the workspace and answer questions."
          "Use fuzzy_find, glob, fuzzy_grep, and read. Report concrete file paths and line-number references, and answer each question explicitly."
          "Return your report with relatedFiles and line ranges." ]

let meditatorPrompt (sections: MeditatorFileSection list) (intent: string) : string =
    let fileItem (s: MeditatorFileSection) : string =
        let body = Option.defaultValue meditatorSkippedSection s.content
        let contentLines = body.Split('\n') |> Array.map (fun line -> "      " + line)

        Array.concat [ [| "  - path: " + yamlStringValue s.file; "    content: |" |]; contentLines ]
        |> String.concat "\n"

    agentPrompt
        [ yamlSeqField "files" (sections |> List.map fileItem)
          yamlField "question" intent ]
        [ "You are in a quiet room with the texts and the question."
          "No tools, no distractions — just you and the problem."
          "Read carefully. Turn it over in your mind."
          "When you are ready, answer with clarity and depth." ]

let browserPrompt (intent: string) : string =
    agentPrompt
        [ yamlField "task" intent ]
        [ "You are a browser automation agent. Use stealth-browser-mcp tools to interact with web pages. Return a detailed report." ]

let executorSummarizerPrompt
    (output: string)
    (language: string)
    (program: string)
    (dependencies: string list)
    (timeoutType: string)
    (mode: string)
    : string =
    agentPrompt
        [ yamlField "language" language
          yamlField "program" program
          yamlStringSeqField "dependencies" dependencies
          yamlField "timeout_type" timeoutType
          yamlField "mode" mode
          yamlField "raw_output" output ]
        [ "You are a filter for executor output. Preserve errors, stack traces, and key paths or values. Omit noise, repeated lines, and progress banners. Do not invent details that are not in the output."
          "Do NOT lose any information." ]

let websearchSummarizerPrompt (whatToSummarize: string) (rawResults: string) : string =
    agentPrompt
        [ yamlField "question" whatToSummarize
          yamlField "raw_results" rawResults ]
        [ "You are a filter for web search results. Focus on answering the question above using the raw results. Preserve concrete facts. Omit boilerplate and unrelated results. Do not invent details not present in the results."
          "Do NOT lose any information." ]
