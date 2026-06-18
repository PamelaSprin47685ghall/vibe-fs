module VibeFs.Opencode.Session

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Message
open VibeFs.Opencode.Actors

let private firstString (ctx: obj) (keys: string list) : string option =
    keys
    |> List.tryPick (fun key ->
        let v = Dyn.get ctx key
        if Dyn.isNullish v then None else Some(string v))

/// Get the abort signal from the opencode context.  The host exposes it as
/// `context.abort` (an AbortSignal), not `context.abortSignal`.
let getAbortSignal (context: obj) : obj =
    if Dyn.isNullish context then null
    else
        let abort = Dyn.get context "abort"
        if Dyn.isNullish abort then null else abort

/// Extract the tool-execution context from an opencode tool `context`.
/// sessionID is returned as null when the host did not provide one, so parent
/// resolution treats the subagent as a top-level session.
let extractToolContext (context: obj) (pluginDirectory: string) : obj =
    let directory = firstString context [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ]
    let sessionID = firstString context [ "sessionID"; "sessionId"; "session_id" ]
    box {|
        directory =
            match directory with
            | Some s when s <> "" -> s
            | _ -> pluginDirectory
        sessionID =
            match sessionID with
            | Some s when s <> "" -> box s
            | _ -> box null
        abortSignal = getAbortSignal context
    |}

/// Dynamically invoke a method chain on ctx.client and await the resulting promise.
let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

/// Convert opencode's message shape `{ info: { role }, parts: [...] }` into the
/// session-entry shape expected by `readAssistantText`.
let private toEntries (messages: obj) : obj array =
    if Dyn.isNullish messages then [||]
    else
        (unbox<obj[]> messages)
        |> Array.map (fun m ->
            let info = Dyn.get m "info"
            let role = if Dyn.isNullish info then null else Dyn.get info "role"
            let parts = Dyn.get m "parts"
            let content = if Dyn.isNullish parts then [||] else unbox<obj[]> parts
            let message = box {| role = role; content = content |}
            box {| ``type`` = "message"; message = message |})

/// Pull the latest assistant text from a session's messages.
let extractSessionText (client: obj) (sessionId: string) (directory: string) : JS.Promise<string> =
    async {
        try
            let arg =
                if directory = "" then
                    box {| path = box {| id = sessionId |} |}
                else
                    box {| path = box {| id = sessionId |}; query = box {| directory = directory |} |}
            let! result = invoke1 arg "messages" (Dyn.get client "session") |> Async.AwaitPromise
            let data = Dyn.get result "data"
            if Dyn.isNullish data then return "(no output)"
            else
                match readAssistantText (toEntries data) None with
                | Some text -> return text
                | None -> return "(no output)"
        with _ -> return "(no output)"
    }
    |> Async.StartAsPromise

let private asPromise<'T> (o: obj) : JS.Promise<'T> = unbox<JS.Promise<'T>> o

[<Global("Promise")>]
let private PromiseCtor : obj = jsNative

let private promiseRace<'T> (promises: JS.Promise<'T> array) : JS.Promise<'T> =
    unbox<JS.Promise<'T>> (PromiseCtor?race(promises))

/// Prompt a session and race it against an AbortSignal.
[<Global>]
type DOMException(message: string, name: string) =
    inherit System.Exception()

let private promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> =
    async {
        let session = Dyn.get client "session"
        if Dyn.isNullish signal then
            do! session?prompt(args) |> asPromise<unit> |> Async.AwaitPromise
        elif Dyn.truthy (Dyn.get signal "aborted") then
            raise (DOMException("Aborted", "AbortError"))
        else
            let settled = ref false
            let handlerRef = ref None
            let abortAsync =
                Async.FromContinuations (fun (cont, _, _) ->
                    let handler = fun () ->
                        if not settled.Value then
                            settled.Value <- true
                            match handlerRef.Value with
                            | Some h -> signal?removeEventListener("abort", h) |> ignore
                            | None -> ()
                            cont "aborted"
                    handlerRef.Value <- Some handler
                    signal?addEventListener("abort", handler) |> ignore)
            let promptAsync =
                async {
                    do! session?prompt(args) |> asPromise<unit> |> Async.AwaitPromise
                    if not settled.Value then
                        settled.Value <- true
                        match handlerRef.Value with
                        | Some h -> signal?removeEventListener("abort", h) |> ignore
                        | None -> ()
                    return "ok"
                }
            try
                let! winner = promiseRace [| promptAsync |> Async.StartAsPromise; abortAsync |> Async.StartAsPromise |] |> Async.AwaitPromise
                if winner = "aborted" then raise (DOMException("Aborted", "AbortError"))
            with err ->
                match translateJsError err with
                | MessageAborted -> raise (DOMException("Aborted", "AbortError"))
                | _ -> raise err
    }
    |> Async.StartAsPromise

/// Core subagent runner. The `cleanup` flag controls whether the child session
/// is aborted and unregistered after the prompt finishes.
let private runSubagentCore (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                            (directory: string) (sessionID: string) (context: obj)
    (tools: obj) (cleanup: bool) : JS.Promise<string> =
    async {
        let parentID = registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)
        let session = Dyn.get client "session"
        let createBody =
            box {|
                query = box {| directory = directory |}
                body = box {|
                    parentID =
                        match parentID with
                        | Some p -> box p
                        | None -> box null
                    title = title
                |}
            |}
        let! createResult = invoke1 createBody "create" session |> Async.AwaitPromise
        let childID = Dyn.str (Dyn.get createResult "data") "id"
        if childID = "" then return "Failed to create child session"
        else
            let abortAndUnregister () =
                if cleanup then
                    let abortPromise : JS.Promise<obj> = invoke1 (box {| path = box {| id = childID |} |}) "abort" session
                    abortPromise |> ignore
                    registry.UnregisterChildAgent(childID)
            registry.RegisterChildAgent(childID, agent, parentID)
            try
                try
                    let promptBody =
                        box {|
                            path = box {| id = childID |}
                            body = box {|
                                agent = agent
                                parts = [| box {| ``type`` = "text"; text = prompt |} |]
                                tools = tools
                            |}
                        |}
                    do! promptWithAbort client promptBody (getAbortSignal context) |> Async.AwaitPromise
                    let! text = extractSessionText client childID directory |> Async.AwaitPromise
                    return if text = "" then "(no output)" else text
                finally
                    abortAndUnregister ()
            with err ->
                match translateJsError err with
                | MessageAborted ->
                    abortAndUnregister ()
                    let! text = extractSessionText client childID directory |> Async.AwaitPromise
                    return if text = "" then "(aborted)" else $"(aborted) {text}"
                | _ -> return raise err
    }
    |> Async.StartAsPromise

/// Run a subagent and keep the child session registered after return.
let runSubagent (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                (directory: string) (sessionID: string) (context: obj)
                (tools: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context tools false

/// Run a subagent and clean up the child session afterwards. Used for
/// short-lived analysis subagents such as the executor summarizer.
let runSubagentWithCleanup (registry: ChildAgentRegistry) (client: obj) (agent: string) (title: string) (prompt: string)
                           (directory: string) (sessionID: string) (context: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context (box null) true

/// Run a subagent with an explicit tool set and clean up afterwards.
let runSubagentWithTools
    (registry: ChildAgentRegistry)
    (client: obj) (agent: string) (title: string) (prompt: string)
    (directory: string) (sessionID: string) (context: obj)
    (tools: obj) : JS.Promise<string> =
    runSubagentCore registry client agent title prompt directory sessionID context tools true

/// Create a reviewer child session under the given parent, register it, and
/// return the child id (empty string on failure).
let createReviewerChild (registry: ChildAgentRegistry) (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                        (directory: string) (parentID: string option)
                        (sessionID: string) (title: string) : JS.Promise<string> =
    async {
        let createBody =
            box {|
                query = box {| directory = directory |}
                body = box {|
                    parentID =
                        match parentID with
                        | Some p -> box p
                        | None -> box null
                    title = title
                |}
            |}
        let! createResult = invoke1 createBody "create" (Dyn.get client "session") |> Async.AwaitPromise
        let childID = Dyn.str (Dyn.get createResult "data") "id"
        if childID <> "" then
            reviewStore.addChild(sessionID, childID)
            registry.RegisterChildAgent(childID, "reviewer", parentID)
        return childID
    }
    |> Async.StartAsPromise

/// Run the reviewer prompt-nudge loop on an existing child session: prompt with
/// the review instructions, wait for the verdict via submit_review_result,
/// nudging up to maxNudges times if the reviewer hasn't submitted.
let runReviewerLoop (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                    (childID: string) (initialParts: obj array) (abortSignal: obj)
                    : JS.Promise<VibeFs.Kernel.ReviewSession.ReviewResult> =
    async {
        let verdict : VibeFs.Kernel.ReviewSession.ReviewResult option ref = ref None
        reviewStore.setPendingReview(childID, (fun r -> verdict.Value <- Some r))
        reviewStore.tryLockReview childID |> ignore
        let maxNudges = 3
        let rec loop nudgeCount =
            async {
                if nudgeCount >= maxNudges then return VibeFs.Kernel.ReviewSession.Terminated
                else
                    let parts =
                        if nudgeCount = 0 then initialParts
                        else [| box {| ``type`` = "text"; text = reviewerNudgePrompt |} |]
                    let promptBody =
                        box {|
                            path = box {| id = childID |}
                            body = box {| agent = "reviewer"; parts = parts; tools = box (createObj [ "return_reviewer", box true ]) |}
                        |}
                    let! caught = Async.Catch (promptWithAbort client promptBody abortSignal |> Async.AwaitPromise)
                    match caught with
                    | Choice2Of2 _ -> return VibeFs.Kernel.ReviewSession.Terminated
                    | Choice1Of2 () ->
                        match verdict.Value with
                        | Some v -> return v
                        | None -> return! loop (nudgeCount + 1)
            }
        let! result = loop 0
        reviewStore.unlockReview childID
        return result
    }
    |> Async.StartAsPromise

/// Run a pre-review session (used by /loop-review): create a reviewer child,
/// prompt it with review instructions + task, wait for the verdict.
let runReviewerSession (registry: ChildAgentRegistry) (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                       (directory: string) (sessionID: string) (task: string)
                       : JS.Promise<VibeFs.Kernel.ReviewSession.ReviewResult> =
    async {
        let parentID = registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)
        let! childID = createReviewerChild registry client reviewStore directory parentID sessionID "Pre-Reviewer" |> Async.AwaitPromise
        if childID = "" then return VibeFs.Kernel.ReviewSession.Terminated
        else
            let parts =
                [| box {| ``type`` = "text"; text = reviewInstructions |}
                   box {| ``type`` = "text"; text = $"=== Task ===\n\n{task}" |} |]
            return! runReviewerLoop client reviewStore childID parts null |> Async.AwaitPromise
    }
    |> Async.StartAsPromise

/// Run a submit-review (used by the submit_review tool): create a reviewer
/// child, prompt it with review instructions + change report + affected files +
/// original task, wait for the verdict.
let runSubmitReview (registry: ChildAgentRegistry) (client: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                    (directory: string) (sessionID: string)
                    (report: string) (affectedFiles: string list)
                    (task: string) (abortSignal: obj)
                    : JS.Promise<VibeFs.Kernel.ReviewSession.ReviewResult> =
    async {
        let parentID = registry.ResolveSubsessionParentID(Some sessionID)
        let! childID = createReviewerChild registry client reviewStore directory parentID sessionID "Reviewer" |> Async.AwaitPromise
        if childID = "" then return VibeFs.Kernel.ReviewSession.Terminated
        else
            let filesText = String.concat "\n" affectedFiles
            let sections = [
                reviewInstructions
                $"=== Change Report ===\n\n{report}"
                $"=== Affected Files ===\n\n{filesText}"
                if task <> "" then $"=== Original Task ===\n\n{task}"
            ]
            let parts = sections |> List.map (fun text -> box {| ``type`` = "text"; text = text |}) |> Array.ofList
            return! runReviewerLoop client reviewStore childID parts abortSignal |> Async.AwaitPromise
    }
    |> Async.StartAsPromise
