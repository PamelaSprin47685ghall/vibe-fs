module VibeFs.Opencode.ToolSchema

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

/// The opencode plugin SDK's `tool` factory + `tool.schema` (Zod-like) builder.
[<Import("tool", "@opencode-ai/plugin/tool")>]
let toolFactory : obj = jsNative
let schema : obj = Dyn.get toolFactory "schema"

/// `o.method()` and `o.method(arg)` - explicit object-first, no pipelines.
let call0 (o: obj) (method: string) : obj = o?(method)()
let call1 (o: obj) (method: string) (arg: obj) : obj = o?(method)(arg)

let private invokeTool (factory: obj) (config: obj) : obj = factory $ config

let private str () = call0 schema "string"
let arr (item: obj) : obj = call1 schema "array" item
let private tuple (items: obj array) : obj = call1 schema "tuple" items
let private union' (items: obj array) : obj = call1 schema "union" items

let strMin (minLen: int) (desc: string) : obj =
    call1 (call1 (str ()) "min" (box minLen)) "describe" (box desc)

let strMinNullish (minLen: int) (desc: string) : obj =
    call1 (call0 (call1 (str ()) "min" (box minLen)) "nullish") "describe" (box desc)

let strReq (desc: string) : obj = call1 (str ()) "describe" (box desc)
let strOpt (desc: string) : obj = call1 (call0 (str ()) "nullish") "describe" (box desc)

let intMinNullish (minVal: int) (desc: string) : obj =
    let n = call1 (call0 schema "number") "int" (box 0)
    let n = call1 n "min" (box minVal)
    call1 (call0 n "nullish") "describe" (box desc)

let boolOpt (desc: string) : obj = call1 (call0 (call0 schema "boolean") "nullish") "describe" (box desc)

let excludeOpt (desc: string) : obj =
    let s = str ()
    call1 (call0 (union' [| s; arr s |]) "nullish") "describe" (box desc)

let private schemaObject (shape: obj) : obj = call1 schema "object" shape

let private strictObject (shape: obj) : obj = call0 (schemaObject shape) "strict"

let private arrayMin (item: obj) (minCount: int) (desc: string) : obj =
    call1 (call1 (arr item) "min" (box minCount)) "describe" (box desc)

let coderIntentsSchema (desc: string) : obj =
    let fileField = strMin 1 "File path to modify."
    let guideField = strMin 1 "Implementation constraints for this file."
    let draftField = strOpt "Optional minimal draft for the coder to reference. Prefer leaving this empty; use only when strict quality needs a concrete sketch. No patch or special format required."
    let targetShape = strictObject (createObj [ "file", fileField; "guide", guideField; "draft", draftField ])
    let targetsField = arrayMin targetShape 1 "Non-empty per-file implementation guides."
    let objectiveField = strMin 1 "Concrete code-change goal for this subagent."
    let backgroundField = strMin 1 "Why this change is needed; prior findings and user context."
    let doNotTouchField = call1 (call0 (arr (strMin 1 "Do-not-touch path, symbol, or constraint.")) "optional") "describe" (box "Optional list of files, directories, symbols, or constraints this subagent must not modify.")
    let inner = strictObject (createObj [ "objective", objectiveField; "background", backgroundField; "do_not_touch", doNotTouchField; "targets", targetsField ])
    arrayMin inner 1 desc

let investigatorIntentsSchema (desc: string) : obj =
    let questionItem = strMin 1 "Question the report must answer."
    let questionsField = arrayMin questionItem 1 "Non-empty list of questions the report must answer explicitly."
    let entryItem = strMin 1 "Optional entry path, symbol, or file."
    let entriesField = call1 (call0 (arr entryItem) "optional") "describe" (box "Optional entry paths, symbols, or files to start from.")
    let objectiveField = strMin 1 "What to investigate in the codebase."
    let backgroundField = strMin 1 "Why this investigation is needed; blockers and prior context."
    let inner =
        strictObject (createObj [ "objective", objectiveField; "background", backgroundField; "questions", questionsField; "entries", entriesField ])
    arrayMin inner 1 desc

let uiParam : obj = call1 (call0 (str ()) "optional") "describe" (box "Internal: populated by hook")
let strArrayReq (desc: string) : obj = call1 (arr (strMin 1 "")) "describe" (box desc)
let strArrayOpt (desc: string) : obj = call1 (call0 (arr (strMin 1 "")) "optional") "describe" (box desc)

let numOpt (desc: string) : obj =
    let n = call0 schema "number"
    let n = call0 n "int"
    let n = call0 n "positive"
    call1 (call0 n "optional") "describe" (box desc)

let enumReq (values: string array) (desc: string) : obj =
    let e = call1 schema "enum" values
    call1 e "describe" (box desc)

let enumOpt (values: string array) (desc: string) : obj =
    let e = call1 schema "enum" values
    call1 (call0 e "optional") "describe" (box desc)

let obj (shape: obj) : obj = call1 schema "object" shape

let define (description: string) (args: obj) (execute: obj -> obj -> JS.Promise<string>) : obj =
    invokeTool toolFactory (box {| description = description; args = args; execute = execute |})

let coder =
    "Execute code changes from structured intents. Each intents[] element spawns its own coder subagent in parallel. Every element must include objective, background, and targets (file + guide per file; optional draft per file); do_not_touch is optional per subagent. "
    + "IMPORTANT: Subagents start in a fresh session with no manager history. Pack all context into background, do_not_touch, and per-file guide fields. Do NOT assume the coder knows the repo."

let investigator =
    "Search the codebase from structured intents. Each intents[] element spawns its own investigator subagent in parallel. Every element must include objective, background, and questions[]; entries[] is optional. "
    + "IMPORTANT: Subagents start in a fresh session with no manager history. Pack context into background and list concrete questions the report must answer. Reports must include file paths."

let meditator =
    "Receive a natural-language intent or question for deep reasoning and delegate to the meditator agent. "
    + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the meditator agent gets its task as a user message built from your intent and files. You (the parent) must put full context into the intent and list every file path the agent needs. Do NOT assume the meditator agent knows the project background."

let browser =
    "Receive a natural-language intent for a web task and delegate to the browser agent. "
    + "IMPORTANT: Subagents do not receive role instructions in their system prompt; the browser agent gets its task as a user message from your intent. You (the parent) must put full context (URLs, goals, constraints) into the intent. Do NOT assume the browser agent knows the project background."

let executor = "Executes a shell command, Python code, or JavaScript/TypeScript program synchronously with a strict timeout budget. On completion (or timeout) the captured output is either returned directly or summarized when it exceeds 8192 bytes. If executing Python or JavaScript, specify dependencies in the \"dependencies\" argument."

let fuzzyFind = "Search for files by fuzzy path text matching. Returns file paths ranked by relevance and frecency. Regex and glob syntax are not supported. Every result ends with iterator=\"...\"; iteration is finished when it becomes iterator=\"\"."

let fuzzyGrep = "Search file contents using fuzzy-aware content search. Smart-case, git-aware, frecency-ranked. Supports automatic regex mode detection. Use mode=fuzzy explicitly for fuzzy matching when exact regex yields no results. Every result ends with iterator=\"...\"; iteration is finished when it becomes iterator=\"\"."

let websearch = "Search the web for any topic; raw results are rewritten by a summarizer subagent focused on what_to_summarize, returning clean, ready-to-use content."

let webfetch = "Fetch a URL with better extraction for static/docs pages. Supports llms.txt probing, content-focused HTML extraction, metadata, and redirects."

module Params =
    let coderIntents =
        "Non-empty array of coder intents. Each item: objective (what to implement), background (why and prior context), optional do_not_touch[] constraints, and targets[] with file, guide, and optional draft per path. One subagent per item, all parallel."

    let coderTdd =
        "TDD phase for this coder call. red = this edit is the RED phase: write the failing test, or the code that fails it; the result must leave tests failing. green = this edit is the GREEN phase: make the failing tests pass. "
        + "Discipline: for a new requirement the requirement comes first; for a bug fix the regression comes first. Always go red before green for any unit of work. "
        + "You MUST issue a tdd=red coder call before any tdd=green coder call for the same work; a green call with no preceding red in the session is a violation and will be rejected. Declare the phase truthfully."

    let investigatorIntents =
        "Non-empty array of investigator intents. Each item: objective, background, questions[] (required KPIs for the report), optional entries[] (paths/symbols to start from). One subagent per item, all parallel."

    let meditatorIntent =
        "Natural-language intent or question for deep reasoning. Becomes part of the subagent user message - include all background, design rationale, and specific requirements; do not assume the agent knows project context."

    let meditatorFiles =
        "File paths listed in the subagent user message for context. Include design docs, relevant code, or background material the agent must read."

    let browserIntent =
        "Natural-language intent for the web task. Becomes the subagent user message - include URLs, goals, constraints, and any project context the browser agent needs."

    let executorLanguage = "Execution language: shell, python, or javascript"
    let executorProgram = "The program to execute."
    let executorDeps = "Dependencies to install (for python or javascript)."
    let executorTimeout = "Execution timeout budget: 'short' (1s), 'long' (10s), or 'last-resort' (100s). Use 'last-resort' only when absolutely necessary."
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
    let websearchWhatToSummarize = "The question or intent the search should answer. The summarizer subagent focuses on extracting and synthesizing content relevant to this."
    let webfetchUrl = "The URL to fetch"
    let webfetchExtractMain = "Extract main content from the page, removing navigation, ads, etc. (default: true)"
    let webfetchPreferLlmsTxt = "Probe for llms.txt files before fetching full page (default: auto)"
    let webfetchPrompt = "Optional extraction task to run on the fetched content using a cheap secondary model"
    let webfetchTimeout = "Timeout in seconds (max: 120)"
