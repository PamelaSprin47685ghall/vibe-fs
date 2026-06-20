module VibeFs.Kernel.ToolCatalog

/// Pure description of a tool: name, prose description shown to the model,
/// per-parameter doc strings, and the required parameter list.  Every adapter
/// (Opencode Zod, Mux JSON Schema, …) consumes the same record.
type ToolSpec =
    { name: string
      description: string
      paramDocs: Map<string, string>
      requiredFields: string list }

let private map (entries: (string * string) list) : Map<string, string> = Map.ofList entries

let private coderSpec : ToolSpec =
    { name = "coder"
      description =
        "Execute code changes from structured intents. Each intents[] element spawns its own coder subagent in parallel. Every element must include objective, background, and targets (file + guide per file; optional draft per file); do_not_touch is optional per subagent. "
        + "IMPORTANT: Subagents start in a fresh session with no manager history. Pack all context into background, do_not_touch, and per-file guide fields. Do NOT assume the coder knows the repo."
      paramDocs =
        map
            [ "intents",
              "Non-empty array of coder intents. Each item: objective (what to implement), background (why and prior context), optional do_not_touch[] constraints, and targets[] with file, guide, and optional draft per path. One subagent per item, all parallel."
              "tdd",
              "TDD phase for this coder call. red = this edit is the RED phase: write the failing test, or the code that fails it; the result must leave tests failing. green = this edit is the GREEN phase: make the failing tests pass. "
              + "Discipline: for a new requirement the requirement comes first; for a bug fix the regression comes first. Always go red before green for any unit of work. "
              + "You MUST issue a tdd=red coder call before any tdd=green coder call for the same work; a green call with no preceding red in the session is usually a violation and tends to be rejected. Declare the phase truthfully." ]
      requiredFields = [ "intents"; "tdd" ] }

let private investigatorSpec : ToolSpec =
    { name = "investigator"
      description =
        "Search the codebase from structured intents. Each intents[] element spawns its own investigator subagent in parallel. Every element must include objective, background, and questions[]; entries[] is optional. "
        + "IMPORTANT: Subagents start in a fresh session with no manager history. Pack context into background and list concrete questions the report must answer. Reports must include file paths."
      paramDocs =
        map
            [ "intents",
              "Non-empty array of investigator intents. Each item: objective, background, questions[] (required KPIs for the report), optional entries[] (paths/symbols to start from). One subagent per item, all parallel." ]
      requiredFields = [ "intents" ] }

let private meditatorSpec : ToolSpec =
    { name = "meditator"
      description =
        "Receive a natural-language intent or question for deep reasoning and delegate to the meditator agent. "
        + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the meditator agent gets its task as a user message built from your intent and files. You (the parent) must put full context into the intent and list every file path the agent needs. Do NOT assume the meditator agent knows the project background."
      paramDocs =
        map
            [ "intent",
              "Natural-language intent or question for deep reasoning. Becomes part of the subagent user message - include all background, design rationale, and specific requirements; do not assume the agent knows project context."
              "files",
              "File paths listed in the subagent user message for context. Include design docs, relevant code, or background material the agent must read." ]
      requiredFields = [ "intent"; "files" ] }

let private browserSpec : ToolSpec =
    { name = "browser"
      description =
        "Receive a natural-language intent for a web task and delegate to the browser agent. "
        + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the browser agent gets its task as a user message from your intent. You (the parent) must put full context (URLs, goals, constraints) into the intent. Do NOT assume the browser agent knows the project background."
      paramDocs =
        map
            [ "intent",
              "Natural-language intent for the web task. Becomes the subagent user message - include URLs, goals, constraints, and any project context the browser agent needs." ]
      requiredFields = [ "intent" ] }

let private executorSpec : ToolSpec =
    { name = "executor"
      description =
        "Executes a shell command, Python code, or JavaScript/TypeScript program synchronously with a strict timeout budget. On completion (or timeout) the captured output is either returned directly or summarized when it exceeds 8192 bytes. If executing Python or JavaScript, specify dependencies in the \"dependencies\" argument."
      paramDocs =
        map
            [ "language", "Execution language: shell, python, or javascript"
              "program", "The program to execute."
              "dependencies", "Dependencies to install (for python or javascript)."
              "timeout_type",
              "Execution timeout budget: 'short' (1s), 'long' (10s), or 'last-resort' (100s). Use 'last-resort' only when absolutely necessary."
              "mode", "Execution mode: 'ro' for read-only/diagnostic/compile/test commands, 'rw' for commands that modify project source files." ]
      requiredFields = [ "language"; "program"; "timeout_type"; "mode" ] }

let private fetchWikiSpec : ToolSpec =
    { name = "fetch_wiki"
      description =
        "Fetch the answer for a project wiki id from this session's wiki snapshot. The manager prelude lists available ids and questions. This tool returns only the answer text and does not read the latest disk wiki."
      paramDocs = map [ "id", "Wiki entry id from the manager prelude." ]
      requiredFields = [ "id" ] }

let private submitWikiSpec : ToolSpec =
    { name = "submit_wiki"
      description =
        "Submit wiki draft entries for the current wiki job context. The host decides whether this is an append, daily rewrite, or weekly rewrite job; entries with an id update existing knowledge, and entries without an id receive a host-assigned id."
      paramDocs = map [ "entries", "Array of wiki draft entries. Each entry: optional id, required q, required a." ]
      requiredFields = [ "entries" ] }

let private fuzzyFindSpec : ToolSpec =
    { name = "fuzzy_find"
      description =
        "Search for files by fuzzy path text matching. Returns file paths ranked by relevance and frecency. Regex and glob syntax are not supported. When more results exist, the response ends with iterator=\"...\"."
      paramDocs =
        map
            [ "pattern", "Initial plain fuzzy file path text to search for."
              "path", "Initial optional path constraint to narrow search scope"
              "limit", "Maximum number of results to return per call (default: 30)"
              "iterator", "Opaque single-use iterator from a previous fuzzy_find result." ]
      requiredFields = [] }

let private fuzzyGrepSpec : ToolSpec =
    { name = "fuzzy_grep"
      description =
        "Search file contents using fuzzy-aware content search. Smart-case, git-aware, frecency-ranked. Supports automatic regex mode detection. Use mode=fuzzy explicitly for fuzzy matching when exact regex yields no results. When more results exist, the response ends with iterator=\"...\"."
      paramDocs =
        map
            [ "pattern", "Initial search pattern. Required on the first call."
              "path", "Initial path constraint."
              "exclude", "Initial exclude paths (e.g. 'test/,*.min.js')"
              "caseSensitive", "Case-sensitivity override (smart-case by default)."
              "context", "Number of context lines before and after each match"
              "limit", "Maximum number of matches to return per call"
              "iterator", "Opaque single-use iterator from a previous fuzzy_grep result." ]
      requiredFields = [] }

let private websearchSpec : ToolSpec =
    { name = "websearch"
      description =
        "Search the web for any topic; raw results are rewritten by a summarizer subagent focused on what_to_summarize, returning clean, ready-to-use content."
      paramDocs =
        map
            [ "query",
              "Natural language search query. Should be a semantically rich description of the ideal page, not just keywords."
              "numResults", "Number of search results to return (default: 10)"
              "what_to_summarize",
              "The question or intent the search should answer. The summarizer subagent focuses on extracting and synthesizing content relevant to this." ]
      requiredFields = [ "query"; "what_to_summarize" ] }

let private webfetchSpec : ToolSpec =
    { name = "webfetch"
      description =
        "Fetch a URL with better extraction for static/docs pages. Supports llms.txt probing, content-focused HTML extraction, metadata, and redirects."
      paramDocs =
        map
            [ "url", "The URL to fetch"
              "extract_main", "Extract main content from the page, removing navigation, ads, etc. (default: true)"
              "prefer_llms_txt", "Probe for llms.txt files before fetching full page (default: auto)"
              "prompt", "Optional extraction task to run on the fetched content using a cheap secondary model"
              "timeout", "Timeout in seconds (max: 120)" ]
      requiredFields = [ "url" ] }

let private submitReviewSpec : ToolSpec =
    { name = "submit_review"
      description =
        "Submit completed work for review. Creates a reviewer sub-agent that examines the changes against evaluation criteria and returns PASS or actionable feedback. Only works when session is in active loop mode."
      paramDocs =
        map
            [ "report", "Detailed report of what was done"
              "affectedFiles", "List of file paths that were modified or created" ]
      requiredFields = [ "report"; "affectedFiles" ] }

let all : ToolSpec list =
    [ coderSpec; investigatorSpec; meditatorSpec; browserSpec; executorSpec
      fetchWikiSpec; submitWikiSpec; fuzzyFindSpec; fuzzyGrepSpec; websearchSpec; webfetchSpec; submitReviewSpec ]

let private byName : Map<string, ToolSpec> = all |> List.map (fun spec -> spec.name, spec) |> Map.ofList

let specOf (name: string) : ToolSpec =
    match Map.tryFind name byName with
    | Some spec -> spec
    | None -> failwithf "ToolCatalog: unknown tool %s" name

let paramDoc (name: string) (field: string) : string =
    let spec = specOf name
    match Map.tryFind field spec.paramDocs with
    | Some doc -> doc
    | None -> failwithf "ToolCatalog: unknown param %s.%s" name field

let description (name: string) : string = (specOf name).description

module Params =
    let private doc tool field = paramDoc tool field

    let coderIntents = doc "coder" "intents"
    let coderTdd = doc "coder" "tdd"
    let investigatorIntents = doc "investigator" "intents"
    let meditatorIntent = doc "meditator" "intent"
    let meditatorFiles = doc "meditator" "files"
    let browserIntent = doc "browser" "intent"
    let executorLanguage = doc "executor" "language"
    let executorProgram = doc "executor" "program"
    let executorDeps = doc "executor" "dependencies"
    let executorTimeout = doc "executor" "timeout_type"
    let executorMode = doc "executor" "mode"
    let fetchWikiId = doc "fetch_wiki" "id"
    let submitWikiEntries = doc "submit_wiki" "entries"
    let fuzzyFindPattern = doc "fuzzy_find" "pattern"
    let fuzzyFindPath = doc "fuzzy_find" "path"
    let fuzzyFindLimit = doc "fuzzy_find" "limit"
    let fuzzyFindIterator = doc "fuzzy_find" "iterator"
    let fuzzyGrepPattern = doc "fuzzy_grep" "pattern"
    let fuzzyGrepPath = doc "fuzzy_grep" "path"
    let fuzzyGrepExclude = doc "fuzzy_grep" "exclude"
    let fuzzyGrepCaseSensitive = doc "fuzzy_grep" "caseSensitive"
    let fuzzyGrepContext = doc "fuzzy_grep" "context"
    let fuzzyGrepLimit = doc "fuzzy_grep" "limit"
    let fuzzyGrepIterator = doc "fuzzy_grep" "iterator"
    let websearchQuery = doc "websearch" "query"
    let websearchNumResults = doc "websearch" "numResults"
    let websearchWhatToSummarize = doc "websearch" "what_to_summarize"
    let webfetchUrl = doc "webfetch" "url"
    let webfetchExtractMain = doc "webfetch" "extract_main"
    let webfetchPreferLlmsTxt = doc "webfetch" "prefer_llms_txt"
    let webfetchPrompt = doc "webfetch" "prompt"
    let webfetchTimeout = doc "webfetch" "timeout"
