module Wanxiangshu.Kernel.ToolCatalog.Subagent

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec
open Wanxiangshu.Kernel

let internal coderSpec: ToolSpec =
    { name = "coder"
      description =
        "Execute code changes from structured intents. Each intents[] element is dispatched to a parallel code-change pipeline that processes the edit automatically. Every element must include objective, background, and targets (file + guide per file; optional draft per file); do_not_touch is optional per item. "
        + "IMPORTANT: Each item is processed independently with no shared state. Pack all context into background, do_not_touch, and per-file guide fields independently. Do NOT assume the processing engine knows your preferences."
      paramDocs =
        map
            [ "intents",
              "Non-empty array of coder intents. Each item is dispatched to its own processing pipeline, all running concurrently."
              "tdd",
              "TDD phase for this coder call. red = this edit is the RED phase: write the failing test, or the code that fails it; the result must leave tests failing. green = this edit is the GREEN phase: make the failing tests pass. "
              + "Discipline: for a new requirement the requirement comes first; for a bug fix the regression comes first. Always go red before green for any unit of work. "
              + "You SHOULD issue a tdd=red coder call before any tdd=green coder call for the same work; a green call with no preceding red in the session is usually a violation and tends to be rejected. Declare the phase truthfully."
              "warn_tdd",
              "TDD discipline acknowledgement: '" + Wanxiangshu.Kernel.WarnTdd.canonicalValue + "' — I confirm I have followed TDD and Kolmolgorov principles, never skipping red phase." ]
      requiredFields = [ "intents"; "tdd"; "warn_tdd" ] }

let internal investigatorSpec: ToolSpec =
    { name = "investigator"
      description =
        "Search the codebase from structured intents. Each intents[] element is dispatched to a parallel search pipeline that processes the query automatically. Every element must include objective, background, and questions[]; entries[] is optional. "
        + "IMPORTANT: Each item is processed independently with no shared state. Pack context into background and list concrete questions the report must answer. Reports must include file paths."
      paramDocs =
        map
            [ "intents",
              "Non-empty array of investigator intents. Each item is dispatched to its own processing pipeline, all running concurrently." ]
      requiredFields = [ "intents" ] }

let internal meditatorSpec: ToolSpec =
    { name = "meditator"
      description =
        "Dispatch a detailed intent or question for deep structured analysis. The analysis engine processes the input and returns a reasoned conclusion. "
        + "IMPORTANT: The engine processes only what you provide — put full context into the intent and list every file path needed. Do NOT assume prior knowledge of the project background."
      paramDocs =
        map
            [ "intent",
              "Natural-language intent or question for deep analysis. Include all background, design rationale, and specific requirements; do not assume prior context."
              "files",
              "File paths provided to the analysis engine. Include design docs, relevant code, or background material." ]
      requiredFields = [ "intent"; "files" ] }

let internal browserSpec: ToolSpec =
    { name = "browser"
      description =
        "Dispatch a web-navigation task for automated processing. The navigation engine executes the task and returns a detailed report. "
        + "IMPORTANT: The engine processes only what you provide — put full context (URLs, goals, constraints) into the intent. Do NOT assume prior knowledge of the project background."
      paramDocs =
        map
            [ "intent",
              "Natural-language intent for the web task. Include URLs, goals, constraints, and any project context needed." ]
      requiredFields = [ "intent" ] }
