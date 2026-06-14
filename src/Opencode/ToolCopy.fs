module VibeFs.Opencode.ToolCopy

/// Tool descriptions — pure strings (the agent-facing surface).
let editor = "Execute code changes from natural-language intents. Provide multiple independent change intents as [intent, affectFiles] tuples; each tuple spawns its own editor subagent session and runs independently in parallel. IMPORTANT: Do NOT assume the editor agent knows the project background. You must provide all necessary context explicitly in each intent."

let greper = "Search the codebase from natural-language intents. Each intent spawns its own search subagent session and runs independently in parallel. IMPORTANT: Do NOT assume the search agent knows the project background. The agent must include a `relatedFiles: [...]` field in its returned report."

let reverie = "Receive a natural-language intent or question for deep reasoning and delegate to the reverie agent. IMPORTANT: Do NOT assume the reverie agent knows the project background."

let browser = "Receive a natural-language intent for a web task and delegate to the browser agent. IMPORTANT: Do NOT assume the browser agent knows the project background."

let executor = "Executes a shell command, Python code, or JavaScript/TypeScript program synchronously with a strict timeout budget. On completion (or timeout) the captured output is either returned directly or summarized when it exceeds 8192 bytes. If executing Python or JavaScript, specify dependencies in the \"dependencies\" argument."

let fuzzyFind = "Search for files by fuzzy path text matching. Returns file paths ranked by relevance and frecency. Regex and glob syntax are not supported. Every result ends with iterator=\"...\"; iteration is finished when it becomes iterator=\"\"."

let fuzzyGrep = "Search file contents using fuzzy-aware content search. Smart-case, git-aware, frecency-ranked. Supports automatic regex mode and automatic fuzzy fallback when no exact matches are found. Every result ends with iterator=\"...\"; iteration is finished when it becomes iterator=\"\"."

let websearch = "Search the web for any topic and get clean, ready-to-use content."

let webfetch = "Fetch a URL with better extraction for static/docs pages. Supports llms.txt probing, content-focused HTML extraction, metadata, and redirects."

/// Param docs (inline strings used by schema builders).
module Params =
    let editorIntents = "Array of [intent, affectFiles] tuples. Each runs its own editor subagent in parallel."
    let greperIntents = "Array of independent code-search intents, each run in parallel."
    let reverieIntent = "A natural-language intent or question to contemplate."
    let reverieFiles = "File paths to provide as context."
    let browserIntent = "A natural-language intent describing the desired web task."
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
