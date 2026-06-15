module VibeFs.Opencode.Tools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.AgentRole
open VibeFs.Kernel.ExecutorKernel
open VibeFs.Kernel.OllamaFormat
open VibeFs.Kernel.ReviewSession
open VibeFs.Shell.OllamaClient
open VibeFs.Opencode.Sdk
open VibeFs.Opencode.ToolCopy
open VibeFs.Opencode.Session
open VibeFs.Kernel.Prompts

let private entry (key: string) (value: obj) : obj = createObj [ key, value ]

let private mergeObjects (objs: obj array) : obj =
    let merged = createObj []
    for item in objs do
        Dyn.assignInto merged item |> ignore
    merged

let private resolveStr (s: string) : JS.Promise<string> = async { return s } |> Async.StartAsPromise

let private optStr (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(string v)
let private optInt (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<int> v)
let private optBool (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<bool> v)
let private optField (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some v

/// The editor tool: each [intent, files] tuple runs its own editor subagent.
let editorTool (ctx: obj) : obj =
    let client = Dyn.get ctx "client"
    define editor
        (box {| intents = intentsSchema Params.editorIntents; _ui = uiParam |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let directory = Dyn.str tc "directory"
            let sessionID = Dyn.str tc "sessionID"
            let intents = Dyn.get args "intents" :?> obj array
            async {
                let! reports =
                    intents |> Array.map (fun intent ->
                        let pair = intent :?> obj array
                        let intentText = string pair.[0]
                        let files = pair.[1] :?> obj array |> Array.map string |> List.ofArray
                        let prompt = formatEditorUserPrompt intentText files
                        runSubagent client (AgentRole.toString Editor) "Editor" prompt directory sessionID context (box null)
                        |> Async.AwaitPromise) |> Async.Parallel
                return String.concat "\n---\n" (List.ofArray reports)
            } |> Async.StartAsPromise)

/// The greper tool: each intent runs its own search subagent in parallel.
let greperTool (ctx: obj) : obj =
    let client = Dyn.get ctx "client"
    define greper
        (box {| intents = call1 (call1 (arr (strMin 1 "")) "min" (box 1)) "describe" (box Params.greperIntents)
                _ui = uiParam |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let intents = Dyn.get args "intents" :?> obj array |> Array.map string
            async {
                let! reports =
                    intents |> Array.map (fun intent ->
                        let prompt = formatGreperUserPrompt intent
                        runSubagent client (AgentRole.toString Greper) "Greper" prompt
                            (Dyn.str tc "directory") (Dyn.str tc "sessionID") context (box null)
                        |> Async.AwaitPromise) |> Async.Parallel
                return String.concat "\n---\n" (List.ofArray reports)
            } |> Async.StartAsPromise)

/// The reverie tool: deep reasoning over provided file context.
let reverieTool (ctx: obj) : obj =
    let client = Dyn.get ctx "client"
    define reverie
        (box {| intent = strReq Params.reverieIntent; files = strArrayOpt Params.reverieFiles |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let directory = Dyn.str tc "directory"
            let sessionID = Dyn.str tc "sessionID"
            let intent = Dyn.str args "intent"
            let files = if Dyn.isNullish (Dyn.get args "files") then [||] else Dyn.get args "files" :?> obj array |> Array.map string
            async {
                let! readResults = VibeFs.Shell.ReverieFiles.readReverieFiles directory (List.ofArray files) |> Async.AwaitPromise
                let sections =
                    Array.map2 (fun file (r: VibeFs.Shell.ReverieFiles.ReverieFileResult) ->
                        { file = file; content = r.content } : HostKernel.ReverieFileSection)
                        files (List.toArray readResults)
                    |> List.ofArray
                let prompt = HostKernel.buildReveriePrompt sections intent
                return! runSubagent client (AgentRole.toString Reverie) "Reverie" prompt
                    directory sessionID context (box null)
                    |> Async.AwaitPromise
            } |> Async.StartAsPromise)

/// The executor tool: run a program with timeout, returning captured output —
/// or a summarizer subagent report when the output exceeds the summary threshold.
let executorTool (ctx: obj) : obj =
    let client = Dyn.get ctx "client"
    define executor
        (box {| language = strReq Params.executorLanguage; program = strReq Params.executorProgram
                dependencies = strArrayOpt Params.executorDeps; timeout_type = strReq Params.executorTimeout |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let lang = match Dyn.str args "language" with "python" -> Python | "javascript" -> Javascript | _ -> Shell
            let timeout = match Dyn.str args "timeout_type" with "long" -> Long | _ -> Short
            let deps = if Dyn.isNullish (Dyn.get args "dependencies") then [] else Dyn.get args "dependencies" :?> obj array |> Array.map string |> List.ofArray
            let options : ExecuteOptions =
                { program = Dyn.str args "program"; language = lang; dependencies = deps
                  timeoutType = timeout; cwd = Some (Dyn.str tc "directory") }
            async {
                let! result = VibeFs.Shell.ExecutorShell.execute options (Dyn.str tc "sessionID") |> Async.AwaitPromise
                let output = match result with Completed o | Truncated(o, _) | Failed o -> o | MissingExecutable(_, o) -> o
                if not (shouldSummarize output) then return output
                else
                    let prompt = formatExecutorSummarizerUserPrompt output
                    return! runSubagentWithCleanup client "summarizer" "Executor summary" prompt
                                (Dyn.str tc "directory") (Dyn.str tc "sessionID") context
                            |> Async.AwaitPromise
            } |> Async.StartAsPromise)

/// The browser tool.
let browserTool (ctx: obj) : obj =
    let client = Dyn.get ctx "client"
    define browser
        (box {| intent = strReq Params.browserIntent |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            runSubagent client (AgentRole.toString Browser) "Browser" (formatBrowserUserPrompt (Dyn.str args "intent"))
                (Dyn.str tc "directory") (Dyn.str tc "sessionID") context (box null))

/// The fuzzy_find tool.
let fuzzyFindTool () : obj =
    define fuzzyFind
        (box {| pattern = strMinNullish 1 Params.fuzzyFindPattern; path = strOpt Params.fuzzyFindPath
                limit = intMinNullish 1 Params.fuzzyFindLimit; iterator = strOpt Params.fuzzyFindIterator |})
        (fun args context ->
            let scopeId = Dyn.str context "sessionID"
            if scopeId = "" then resolveStr "Error: fuzzy_find requires an active session"
            else
                let p : VibeFs.Shell.FuzzyCoordinator.FuzzyFindParams =
                    { pattern = optStr args "pattern"; path = optStr args "path"
                      limit = optInt args "limit"; iterator = optStr args "iterator" }
                let o : VibeFs.Shell.FuzzyCoordinator.SearchOptions = { cwd = Dyn.str context "directory"; scopeId = scopeId; store = None }
                async { let! r = VibeFs.Shell.FuzzyFindCmd.fuzzyFind p o |> Async.AwaitPromise in return r.output } |> Async.StartAsPromise)

/// The fuzzy_grep tool.
let fuzzyGrepTool () : obj =
    define fuzzyGrep
        (box {| pattern = strMinNullish 1 Params.fuzzyGrepPattern; path = strOpt Params.fuzzyGrepPath
                exclude = excludeOpt Params.fuzzyGrepExclude; caseSensitive = boolOpt Params.fuzzyGrepCaseSensitive
                context = intMinNullish 0 Params.fuzzyGrepContext; limit = intMinNullish 1 Params.fuzzyGrepLimit
                iterator = strOpt Params.fuzzyGrepIterator |})
        (fun args context ->
            let scopeId = Dyn.str context "sessionID"
            if scopeId = "" then resolveStr "Error: fuzzy_grep requires an active session"
            else
                let p : VibeFs.Shell.FuzzyCoordinator.FuzzyGrepParams =
                    { pattern = optStr args "pattern"; path = optStr args "path"; exclude = optField args "exclude"
                      caseSensitive = optBool args "caseSensitive"; context = optInt args "context"
                      limit = optInt args "limit"; iterator = optStr args "iterator" }
                let o : VibeFs.Shell.FuzzyCoordinator.SearchOptions = { cwd = Dyn.str context "directory"; scopeId = scopeId; store = None }
                async { let! r = VibeFs.Shell.FuzzyGrepCmd.fuzzyGrep p o |> Async.AwaitPromise in return r.output } |> Async.StartAsPromise)

let private abortSignal (context: obj) : obj =
    if Dyn.isNullish context then null else Dyn.get context "abort"

/// Search the web via the Ollama web_search endpoint.
let websearchTool () : obj =
    define websearch
        (box {| query = strReq Params.websearchQuery
                numResults = numOpt Params.websearchNumResults |})
        (fun args context ->
            let query = Dyn.str args "query"
            if query = "" then resolveStr "Error: query is required"
            else
                let signal = abortSignal context
                async {
                    try
                        let numResults = defaultArg (optInt args "numResults") 10
                        let body = createObj [ "query", box query; "max_results", box numResults ]
                        let! data = ollamaPost "web_search" body (if Dyn.isNullish signal then None else Some signal) |> Async.AwaitPromise
                        let results = Dyn.get data "results"
                        let items =
                            if Dyn.isNullish results || not (Dyn.isArray results) then []
                            else (results :?> obj array) |> Array.map (fun r -> { title = Dyn.str r "title"; url = Dyn.str r "url"; content = Dyn.str r "content" }) |> List.ofArray
                        return formatSearchResults items
                    with ex -> return $"Search failed: {ex.Message}"
                } |> Async.StartAsPromise)

/// Fetch a URL via the Ollama web_fetch endpoint.
let webfetchTool () : obj =
    define webfetch
        (box {| url = strReq Params.webfetchUrl
                extract_main = boolOpt Params.webfetchExtractMain
                prefer_llms_txt = enumOpt [| "auto"; "always"; "never" |] Params.webfetchPreferLlmsTxt
                prompt = strOpt Params.webfetchPrompt
                timeout = numOpt Params.webfetchTimeout |})
        (fun args context ->
            let url = Dyn.str args "url"
            if url = "" then resolveStr "Error: url is required"
            else
                let signal = abortSignal context
                async {
                    let! urlError = validateFetchUrl url |> Async.AwaitPromise
                    match urlError with
                    | Some e -> return e
                    | None ->
                        let bodyEntries = ResizeArray<(string * obj)>()
                        bodyEntries.Add("url", box url)
                        match optBool args "extract_main" with Some v -> bodyEntries.Add("extract_main", box v) | None -> ()
                        match optStr args "prefer_llms_txt" with Some v -> bodyEntries.Add("prefer_llms_txt", box v) | None -> ()
                        match optStr args "prompt" with Some v -> bodyEntries.Add("prompt", box v) | None -> ()
                        match optInt args "timeout" with Some v -> bodyEntries.Add("timeout", box v) | None -> ()
                        let body = createObj (Seq.toList bodyEntries)
                        try
                            let! data = ollamaPost "web_fetch" body (if Dyn.isNullish signal then None else Some signal) |> Async.AwaitPromise
                            let title = if Dyn.isNullish (Dyn.get data "title") then None else Some (Dyn.str data "title")
                            let byline = if Dyn.isNullish (Dyn.get data "byline") then None else Some (Dyn.str data "byline")
                            let length_ = if Dyn.isNullish (Dyn.get data "length") then None else Some (unbox<int> (Dyn.get data "length"))
                            let content = if Dyn.isNullish (Dyn.get data "content") then None else Some (Dyn.str data "content")
                            return formatFetchResponse { title = title; byline = byline; length = length_; content = content }
                        with ex -> return $"Web fetch failed: {ex.Message}"
                } |> Async.StartAsPromise)

/// Format a reviewer verdict into the orchestrator-facing tool output.
let private formatReviewResult (result: VibeFs.Kernel.ReviewSession.ReviewResult) : string =
    match result with
    | VibeFs.Kernel.ReviewSession.Accepted ->
        "Review passed. Your changes have been accepted. loop mode has ended."
    | VibeFs.Kernel.ReviewSession.Terminated -> "Review terminated."
    | VibeFs.Kernel.ReviewSession.Rejected feedback ->
        "Review feedback:\n\n" + feedback
        + "\n\nAddress the feedback above. loop mode is still active — fix the issues and call submit_review again."

/// submit_review: submit work for review. Creates a reviewer sub-agent that
/// inspects the changes against the original task and returns PASS or
/// actionable feedback. Only works when session is in active loop mode.
let submitReviewTool (ctx: obj) (store: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj =
    let client = Dyn.get ctx "client"
    define "Submit your work for review (loop mode)."
        (box {| report = strReq "Detailed report of what you did"; affectedFiles = strArrayOpt "Files you modified" |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let sessionID = Dyn.str tc "sessionID"
            if sessionID = "" || not (store.isReviewActive sessionID) then
                resolveStr "You do not need review. Just continue with your work."
            elif not (store.tryLockReview sessionID) then
                resolveStr "A review is already in progress. Wait for it to finish."
            else
                let report = Dyn.str args "report"
                let affectedFiles =
                    if Dyn.isNullish (Dyn.get args "affectedFiles") then []
                    else Dyn.get args "affectedFiles" :?> obj array |> Array.map string |> List.ofArray
                let abort = Dyn.get tc "abortSignal"
                async {
                    try
                        let task = defaultArg (store.getReviewTask sessionID) ""
                        let! result = runSubmitReview client store (Dyn.str tc "directory") sessionID report affectedFiles task abort |> Async.AwaitPromise
                        match result with
                        | VibeFs.Kernel.ReviewSession.Accepted
                        | VibeFs.Kernel.ReviewSession.Terminated ->
                            store.deactivateReview sessionID
                        | VibeFs.Kernel.ReviewSession.Rejected _ -> ()
                        return formatReviewResult result
                    finally
                        store.unlockReview sessionID
                } |> Async.StartAsPromise)

/// submit_review_result: submit the reviewer's verdict.
let submitReviewResultTool (store: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj =
    define "Submit your review verdict."
        (box {| feedback = strOpt "null to accept, or specific rejection feedback" |})
        (fun args context ->
            let sessionID =
                let id = Dyn.str context "sessionID"
                if id = "" then "loop" else id
            let result =
                match optStr args "feedback" with
                | None -> Accepted
                | Some f ->
                    let trimmed = f.Trim()
                    if trimmed = "" then Accepted else Rejected trimmed
            async { return if store.resolvePendingReview (sessionID, result) then "Verdict submitted." else "No active review to resolve." } |> Async.StartAsPromise)

/// Build the full tool map: name → ToolDefinition, as a plain JS object.
let createTools (ctx: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : obj =
    mergeObjects [|
        entry "editor" (editorTool ctx); entry "greper" (greperTool ctx)
        entry "reverie" (reverieTool ctx); entry "browser" (browserTool ctx)
        entry "executor" (executorTool ctx)
        entry "fuzzy_find" (fuzzyFindTool ()); entry "fuzzy_grep" (fuzzyGrepTool ())
        entry "websearch" (websearchTool ()); entry "webfetch" (webfetchTool ())
        entry "submit_review" (submitReviewTool ctx reviewStore)
        entry "submit_review_result" (submitReviewResultTool reviewStore)
    |]
