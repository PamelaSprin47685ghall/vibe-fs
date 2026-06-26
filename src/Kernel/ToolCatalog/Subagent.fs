module Wanxiangshu.Kernel.ToolCatalog.Subagent

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec

let internal coderSpec: ToolSpec =
    { name = "coder"
      description =
        "Execute code changes from structured intents. Each intents[] element spawns its own coder subagent in parallel. Every element must include objective, background, and targets (file + guide per file; optional draft per file); do_not_touch is optional per subagent. "
        + "IMPORTANT: Subagents start in a fresh session with no manager history. Pack all context into background, do_not_touch, and per-file guide fields independently. Do NOT assume the coder knows your preferences."
      paramDocs =
        map
            [ "intents",
              "Non-empty array of coder intents. Each item: objective, background (handover documentation), optional do_not_touch[] constraints, and targets[] with file, guide, and optional draft per path. One subagent per item, all parallel."
              "tdd",
              "TDD phase for this coder call. red = this edit is the RED phase: write the failing test, or the code that fails it; the result must leave tests failing. green = this edit is the GREEN phase: make the failing tests pass. "
              + "Discipline: for a new requirement the requirement comes first; for a bug fix the regression comes first. Always go red before green for any unit of work. "
              + "You SHOULD issue a tdd=red coder call before any tdd=green coder call for the same work; a green call with no preceding red in the session is usually a violation and tends to be rejected. Declare the phase truthfully." ]
      requiredFields = [ "intents"; "tdd" ] }

let internal investigatorSpec: ToolSpec =
    { name = "investigator"
      description =
        "Search the codebase from structured intents. Each intents[] element spawns its own investigator subagent in parallel. Every element must include objective, background, and questions[]; entries[] is optional. "
        + "IMPORTANT: Subagents start in a fresh session with no manager history. Pack context into background and list concrete questions the report must answer. Reports must include file paths."
      paramDocs =
        map
            [ "intents",
              "Non-empty array of investigator intents. Each item: objective, background, questions[], optional entries[] (paths/symbols to start from). One subagent per item, all parallel." ]
      requiredFields = [ "intents" ] }

let internal meditatorSpec: ToolSpec =
    { name = "meditator"
      description =
        "Receive a detailed intent or question for deep reasoning and delegate to the meditator agent. "
        + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the meditator agent gets its task as a user message built from your intent and files. You (the parent) must put full context into the intent and list every file path the agent needs. Do NOT assume the meditator agent knows the project background."
      paramDocs =
        map
            [ "intent",
              "Natural-language intent or question for deep reasoning. Becomes part of the subagent user message - include all background, design rationale, and specific requirements; do not assume the agent knows project context."
              "files",
              "File paths listed in the subagent user message for context. Include design docs, relevant code, or background material the agent must read." ]
      requiredFields = [ "intent"; "files" ] }

let internal browserSpec: ToolSpec =
    { name = "browser"
      description =
        "Receive a natural-language intent for a web task and delegate to the browser agent. "
        + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the browser agent gets its task as a user message from your intent. You (the parent) must put full context (URLs, goals, constraints) into the intent. Do NOT assume the browser agent knows the project background."
      paramDocs =
        map
            [ "intent",
              "Natural-language intent for the web task. Becomes the subagent user message - include URLs, goals, constraints, and any project context the browser agent needs." ]
      requiredFields = [ "intent" ] }
