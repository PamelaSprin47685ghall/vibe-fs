module VibeFs.Opencode.ToolCopy

/// Tool descriptions — pure strings (the agent-facing surface).
let editor =
    "Execute code changes from natural-language intents. Provide multiple independent change intents as [intent, affectFiles] tuples; each tuple spawns its own editor subagent session and runs independently in parallel — pass as many tuples as you can at once so they execute concurrently. "
    + "IMPORTANT: Subagents do not receive role instructions in their system prompt; each subagent gets its task as a user message built from your intent and file list. You (the parent) must put full project context, design rationale, and requirements into every intent. Do NOT assume the editor knows the repo background."

let greper =
    "Search the codebase from natural-language intents. Each intent in the array spawns its own search subagent session and runs independently in parallel — pass as many intents as you can at once so they execute concurrently. "
    + "IMPORTANT: Subagents do not receive role instructions in their system prompt; each subagent gets its task as a user message from your intent string. You (the parent) must put full context into every intent. Do NOT assume the search agent knows the project background. Reports must include concrete file paths (for example via agent_report structuredOutput relatedFiles)."

let reverie =
    "Receive a natural-language intent or question for deep reasoning and delegate to the reverie agent. "
    + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the reverie agent gets its task as a user message built from your intent and files. You (the parent) must put full context into the intent and list every file path the agent needs. Do NOT assume the reverie agent knows the project background."

let browser =
    "Receive a natural-language intent for a web task and delegate to the browser agent. "
    + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the browser agent gets its task as a user message from your intent. You (the parent) must put full context (URLs, goals, constraints) into the intent. Do NOT assume the browser agent knows the project background."

let executor = "Executes a shell command, Python code, or JavaScript/TypeScript program synchronously with a strict timeout budget. On completion (or timeout) the captured output is either returned directly or summarized when it exceeds 8192 bytes. If executing Python or JavaScript, specify dependencies in the \"dependencies\" argument."

let fuzzyFind = "Search for files by fuzzy path text matching. Returns file paths ranked by relevance and frecency. Regex and glob syntax are not supported. Every result ends with iterator=\"...\"; iteration is finished when it becomes iterator=\"\"."

let fuzzyGrep = "Search file contents using fuzzy-aware content search. Smart-case, git-aware, frecency-ranked. Supports automatic regex mode and automatic fuzzy fallback when no exact matches are found. Every result ends with iterator=\"...\"; iteration is finished when it becomes iterator=\"\"."

let websearch = "Search the web for any topic and get clean, ready-to-use content."

let webfetch = "Fetch a URL with better extraction for static/docs pages. Supports llms.txt probing, content-focused HTML extraction, metadata, and redirects."

/// Param docs (inline strings used by schema builders).
module Params =
    let editorIntents =
        "Array of [intent, affectFiles] tuples. Each tuple is a natural-language change request plus affected file paths; each tuple runs in parallel via its own editor subagent. The intent string is delivered to the subagent as the user message — include all background, design rationale, and requirements there."
    let greperIntents =
        "Array of independent code-search intent strings, each run in parallel via its own search subagent. Each string becomes the subagent user message — include background, paths, symbols, and what to find."
    let reverieIntent =
        "Natural-language intent or question for deep reasoning. Becomes part of the subagent user message — include all background, design rationale, and specific requirements; do not assume the agent knows project context."
    let reverieFiles =
        "File paths listed in the subagent user message for context. Include design docs, relevant code, or background material the agent must read."
    let browserIntent =
        "Natural-language intent for the web task. Becomes the subagent user message — include URLs, goals, constraints, and any project context the browser agent needs."
    let executorLanguage = "Execution language: shell, python, or javascript"
    let executorProgram = "The program to execute."
    let executorDeps = "Dependencies to install (for python or javascript)."
    let executorTimeout = "Execution timeout budget: 'short' (1s) or 'long' (10s)."
    let fuzzyFindPattern = "Initial plain fuzzy file path text to search for."
    let fuzzyFindPath = "Initial optional path constraint to narrow search scope"
    let fuzzyFindLimit = "Maximum number of results to return per call (default: 30)"
    let fuzzyFindIterator = "Opaque single-use iterator from a previous fuzzy_find result."
    let fuzzyGrepPattern = "Initial search pattern. Required on the first call."
    let fuzzyGrepPath = "Initial path constraint."
    let fuzzyGrepExclude = "Initial exclude paths (e.g. 'test/,*.min.js')"
    let fuzzyGrepCaseSensitive = "Case-sensitivity override (smart-case by default)."
    let fuzzyGrepContext = "Number of context lines before and after each match"
    let fuzzyGrepLimit = "Maximum number of matches to return per call"
    let fuzzyGrepIterator = "Opaque single-use iterator from a previous fuzzy_grep result."
    let websearchQuery = "Natural language search query. Should be a semantically rich description of the ideal page, not just keywords."
    let websearchNumResults = "Number of search results to return (default: 10)"
    let webfetchUrl = "The URL to fetch"
    let webfetchExtractMain = "Extract main content from the page, removing navigation, ads, etc. (default: true)"
    let webfetchPreferLlmsTxt = "Probe for llms.txt files before fetching full page (default: auto)"
    let webfetchPrompt = "Optional extraction task to run on the fetched content using a cheap secondary model"
    let webfetchTimeout = "Timeout in seconds (max: 120)"
