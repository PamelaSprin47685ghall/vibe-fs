module VibeFs.Opencode.Tools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Executor
open VibeFs.Kernel.Fuzzy
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.ToolCatalog
open VibeFs.Shell.OllamaClient
open VibeFs.Opencode.ToolSchema
open VibeFs.Opencode.SessionIo
open VibeFs.Opencode.ReviewerLoop
open VibeFs.Opencode.WikiTools
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzySearch
module ToolSchemaModule = VibeFs.Opencode.ToolSchema
module FuzzyCommandsModule = VibeFs.Shell.FuzzySearch

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")

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

let coderTool (registry: ChildAgentRegistry) (wikiRuntime: VibeFs.Opencode.WikiRuntime.WikiRuntime) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define coder
        (box {| intents = coderIntentsSchema Params.coderIntents; tdd = enumReq [| "red"; "green" |] Params.coderTdd; _ui = uiParam |})
        (fun args context ->
            match parseCoderIntents (Dyn.get args "intents") with
            | Error message -> resolveStr message
            | Ok intents ->
                let tc = extractToolContext context (Dyn.str ctx "directory")
                let directory = Dyn.str tc "directory"
                let sessionID = Dyn.str tc "sessionID"
                let prompts = formatPrompt opencode (Coder intents)
                async {
                    let! reports =
                        prompts
                        |> List.map (fun prompt ->
                            runSubagentWithEffect
                                registry
                                (client ())
                                "coder"
                                "Coder"
                                prompt
                                directory
                                sessionID
                                context
                                (box null)
                                Rw
                                (Some (fun record -> wikiRuntime.StartBookkeeperAppend(record.prompt, record.result, record.title, record.prompt)))
                            |> Async.AwaitPromise)
                        |> Async.Parallel
                    return joinReports reports
                } |> Async.StartAsPromise)

let investigatorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define investigator
        (box {| intents = investigatorIntentsSchema Params.investigatorIntents
                _ui = uiParam |})
        (fun args context ->
            match parseInvestigatorIntents (Dyn.get args "intents") with
            | Error message -> resolveStr message
            | Ok intents ->
                let tc = extractToolContext context (Dyn.str ctx "directory")
                let prompts = formatPrompt opencode (Investigator intents)
                async {
                    let! reports =
                        prompts
                        |> List.map (fun prompt ->
                            runSubagent registry (client ()) "investigator" "Investigator" prompt
                                (Dyn.str tc "directory") (Dyn.str tc "sessionID") context (box null)
                            |> Async.AwaitPromise)
                        |> Async.Parallel
                    return joinReports reports
                } |> Async.StartAsPromise)

let meditatorTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define meditator
        (box {| intent = strReq Params.meditatorIntent; files = strArrayOpt Params.meditatorFiles |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let directory = Dyn.str tc "directory"
            let sessionID = Dyn.str tc "sessionID"
            let intent = Dyn.str args "intent"
            let files = if Dyn.isNullish (Dyn.get args "files") then [||] else Dyn.get args "files" :?> obj array |> Array.map string
            async {
                let! readResults = VibeFs.Shell.WorkspaceFiles.readReverieFiles directory (List.ofArray files) |> Async.AwaitPromise
                let sections =
                    Array.map2 (fun file (r: VibeFs.Shell.WorkspaceFiles.ReverieFileResult) ->
                        { file = file; content = r.content } : MeditatorFileSection)
                        files (List.toArray readResults)
                    |> List.ofArray
                let prompt = formatPrompt opencode (Meditator(intent, sections)) |> List.head
                return! runSubagent registry (client ()) "meditator" "Meditator" prompt
                    directory sessionID context (box null)
                    |> Async.AwaitPromise
            } |> Async.StartAsPromise)

let executorTool (registry: ChildAgentRegistry) (wikiRuntime: VibeFs.Opencode.WikiRuntime.WikiRuntime) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define executor
        (box {| language = strReq Params.executorLanguage; program = strReq Params.executorProgram
                dependencies = strArrayOpt Params.executorDeps; timeout_type = strReq Params.executorTimeout
                mode = enumReq [| "ro"; "rw" |] Params.executorMode |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            let sessionID = Dyn.str tc "sessionID"
            post sessionID (fun () ->
                let lang = parseLanguage (Dyn.str args "language")
                let timeout = parseTimeout (Dyn.str args "timeout_type")
                let mode = Dyn.str args "mode"
                let deps = if Dyn.isNullish (Dyn.get args "dependencies") then [] else Dyn.get args "dependencies" :?> obj array |> Array.map string |> List.ofArray
                let options : ExecuteOptions =
                    { program = Dyn.str args "program"; language = lang; dependencies = deps
                      timeoutType = timeout; cwd = Some (Dyn.str tc "directory") }
                async {
                    let! result = VibeFs.Shell.Executor.execute options sessionID |> Async.AwaitPromise
                    let output = match result with Completed o | Truncated(o, _) | Failed o -> o | MissingExecutable(_, o) -> o
                    let! finalOutput =
                        if not (shouldSummarize byteLength output) then
                            async { return prependSafetyWarningForExecution output options }
                        else
                            async {
                                let prompt = formatPrompt opencode (ExecutorSummary output) |> List.head
                                let! summary =
                                    runSubagentWithCleanup registry (client ()) "executor" "Executor summary" prompt
                                        (Dyn.str tc "directory") sessionID context
                                    |> Async.AwaitPromise
                                return prependSafetyWarningForExecution summary options
                            }
                    if mode = "rw" then wikiRuntime.StartBookkeeperAppend(Dyn.str args "program", finalOutput, "Executor", Dyn.str args "program")
                    return finalOutput
                } |> Async.StartAsPromise))

let browserTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define browser
        (box {| intent = strReq Params.browserIntent |})
        (fun args context ->
            let tc = extractToolContext context (Dyn.str ctx "directory")
            runSubagent registry (client ()) "browser" "Browser" (formatPrompt opencode (Browser(Dyn.str args "intent")) |> List.head)
                (Dyn.str tc "directory") (Dyn.str tc "sessionID") context (box null))

let fuzzyFindTool (finderCache: FinderCache) : obj =
    define ToolSchemaModule.fuzzyFind
        (box {| pattern = strMinNullish 1 Params.fuzzyFindPattern; path = strOpt Params.fuzzyFindPath
                limit = intMinNullish 1 Params.fuzzyFindLimit; iterator = strOpt Params.fuzzyFindIterator |})
        (fun args context ->
            let scopeId = Dyn.str context "sessionID"
            if scopeId = "" then resolveStr "Error: fuzzy_find requires an active session"
            else
                let p : FuzzyFindParams =
                    { pattern = optStr args "pattern"
                      path = optStr args "path"
                      limit = optInt args "limit"
                      iterator = optStr args "iterator" }
                let o : SearchOptions =
                    { cwd = Dyn.str context "directory"
                      scopeId = scopeId
                      store = None
                      finderCache = finderCache }
                async {
                    let! r = FuzzyCommandsModule.fuzzyFind p o |> Async.AwaitPromise
                    return r.output
                }
                |> Async.StartAsPromise)

let fuzzyGrepTool (finderCache: FinderCache) : obj =
    define ToolSchemaModule.fuzzyGrep
        (box {| pattern = strMinNullish 1 Params.fuzzyGrepPattern; path = strOpt Params.fuzzyGrepPath
                exclude = excludeOpt Params.fuzzyGrepExclude; caseSensitive = boolOpt Params.fuzzyGrepCaseSensitive
                context = intMinNullish 0 Params.fuzzyGrepContext; limit = intMinNullish 1 Params.fuzzyGrepLimit
                iterator = strOpt Params.fuzzyGrepIterator |})
        (fun args context ->
            let scopeId = Dyn.str context "sessionID"
            if scopeId = "" then resolveStr "Error: fuzzy_grep requires an active session"
            else
                let p : FuzzyGrepParams =
                    { pattern = optStr args "pattern"
                      path = optStr args "path"
                      exclude = parseExcludeField args
                      caseSensitive = optBool args "caseSensitive"
                      context = optInt args "context"
                      limit = optInt args "limit"
                      iterator = optStr args "iterator" }
                let o : SearchOptions =
                    { cwd = Dyn.str context "directory"
                      scopeId = scopeId
                      store = None
                      finderCache = finderCache }
                async {
                    let! r = FuzzyCommandsModule.fuzzyGrep p o |> Async.AwaitPromise
                    return r.output
                }
                |> Async.StartAsPromise)

let private abortSignal (context: obj) : obj =
    if Dyn.isNullish context then null else Dyn.get context "abort"

let websearchTool (registry: ChildAgentRegistry) (ctx: obj) : obj =
    let client () = Dyn.get ctx "client"
    define websearch
        (box {| query = strReq Params.websearchQuery
                numResults = numOpt Params.websearchNumResults
                what_to_summarize = strReq Params.websearchWhatToSummarize |})
        (fun args context ->
            let query = Dyn.str args "query"
            let whatToSummarize = Dyn.str args "what_to_summarize"
            if query = "" then resolveStr "Error: query is required"
            elif whatToSummarize = "" then resolveStr "Error: what_to_summarize is required"
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
                        let rawResults = formatSearchResults items
                        if items.IsEmpty then return rawResults
                        else
                            let tc = extractToolContext context (Dyn.str ctx "directory")
                            let prompt = formatPrompt opencode (WebsearchSummary(whatToSummarize, rawResults)) |> List.head
                            return! runSubagentWithCleanup registry (client ()) "executor" "Web search summary" prompt
                                        (Dyn.str tc "directory") (Dyn.str tc "sessionID") context
                                    |> Async.AwaitPromise
                    with ex -> return $"Search failed: {ex.Message}"
                } |> Async.StartAsPromise)

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

let private formatReviewResult = VibeFs.Kernel.Prompts.formatReviewResult

let submitReviewTool (registry: ChildAgentRegistry) (ctx: obj) (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    let client () = Dyn.get ctx "client"
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
                        let! result = runSubmitReview registry (client ()) store (Dyn.str tc "directory") sessionID report affectedFiles task abort |> Async.AwaitPromise
                        match result with
                        | Accepted
                        | Terminated ->
                            store.deactivateReview sessionID
                        | Rejected _ -> ()
                        return formatReviewResult result
                    finally
                        store.unlockReview sessionID
                } |> Async.StartAsPromise)

let submitReviewResultTool (store: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
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

let createTools (registry: ChildAgentRegistry) (finderCache: FinderCache) (ctx: obj) (wikiRuntime: VibeFs.Opencode.WikiRuntime.WikiRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : obj =
    mergeObjects [|
        entry "coder" (coderTool registry wikiRuntime ctx); entry "investigator" (investigatorTool registry ctx)
        entry "meditator" (meditatorTool registry ctx); entry "browser" (browserTool registry ctx)
        entry "executor" (executorTool registry wikiRuntime ctx)
        entry "fuzzy_find" (fuzzyFindTool finderCache); entry "fuzzy_grep" (fuzzyGrepTool finderCache)
        entry "websearch" (websearchTool registry ctx); entry "webfetch" (webfetchTool ())
        entry "fetch_wiki" (fetchWikiTool wikiRuntime ctx); entry "submit_wiki" (submitWikiTool wikiRuntime)
        entry "submit_review" (submitReviewTool registry ctx reviewStore)
        entry "return_reviewer" (submitReviewResultTool reviewStore)
    |]
