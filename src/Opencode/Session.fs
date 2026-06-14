module VibeFs.Opencode.Session

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.SessionText

let private firstString (ctx: obj) (keys: string list) : string option =
    keys
    |> List.tryPick (fun key ->
        let v = Dyn.get ctx key
        if Dyn.isNullish v then None else Some(string v))

/// Get the abort signal from the opencode context.  The host exposes it as
/// `context.abort` (an AbortSignal), not `context.abortSignal`.
let private getAbortSignal (context: obj) : obj =
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
[<Emit("$2[$1]($0)")>]
let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = jsNative

/// Convert opencode's message shape `{ info: { role }, parts: [...] }` into the
/// session-entry shape expected by `readAssistantText`.
[<Emit("($0 || []).map(function(m) { return { type: 'message', message: { role: m.info && m.info.role, content: m.parts || [] } }; })")>]
let private toEntries (messages: obj) : obj array = jsNative

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

/// Prompt a session and race it against an AbortSignal.
[<Emit("""
(function(client, args, signal) {
  const session = client.session;
  if (signal && signal.aborted) {
    return Promise.reject(new DOMException('Aborted', 'AbortError'));
  }
  if (!signal) {
    return session.prompt(args).then(function() {});
  }
  return new Promise(function(resolve, reject) {
    let settled = false;
    function onAbort() {
      if (settled) return;
      settled = true;
      signal.removeEventListener('abort', onAbort);
      reject(new DOMException('Aborted', 'AbortError'));
    }
    signal.addEventListener('abort', onAbort);
    session.prompt(args).then(function() {
      if (!settled) { settled = true; signal.removeEventListener('abort', onAbort); resolve(); }
    }, function(err) {
      if (!settled) { settled = true; signal.removeEventListener('abort', onAbort); reject(err); }
    });
  });
})(""" + "$0" + "," + "$1" + "," + "$2" + ")")>]
let private promptWithAbort (client: obj) (args: obj) (signal: obj) : JS.Promise<unit> = jsNative

/// Core subagent runner. When cleanup is true, the child session is aborted and
/// unregistered after the prompt finishes so temporary subagents do not linger.
let private runSubagentCore (client: obj) (agent: string) (title: string) (prompt: string)
                            (directory: string) (sessionID: string) (context: obj)
                            (cleanup: bool) : JS.Promise<string> =
    async {
        let parentID = ChildAgent.resolveSubsessionParentID (if sessionID = "" then None else Some sessionID)
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
            let mutable cleanedUp = false
            let abortAndUnregister () =
                if not cleanedUp then
                    cleanedUp <- true
                    let abortPromise : JS.Promise<obj> = invoke1 (box {| path = box {| id = childID |} |}) "abort" session
                    abortPromise |> ignore
                    ChildAgent.unregisterChildAgent childID
            ChildAgent.registerChildAgent childID agent parentID
            try
                try
                    let promptBody =
                        box {|
                            path = box {| id = childID |}
                            body = box {|
                                agent = agent
                                parts = [| box {| ``type`` = "text"; text = prompt |} |]
                            |}
                        |}
                    do! promptWithAbort client promptBody (getAbortSignal context) |> Async.AwaitPromise
                    let! text = extractSessionText client childID directory |> Async.AwaitPromise
                    return if text = "" then "(no output)" else text
                finally
                    if cleanup then abortAndUnregister ()
            with err ->
                if AbortKernel.isAbortError err then
                    abortAndUnregister ()
                    let! text = extractSessionText client childID directory |> Async.AwaitPromise
                    return if text = "" then "(aborted)" else $"(aborted) {text}"
                else return raise err
    }
    |> Async.StartAsPromise

/// Run a subagent: resolve the parent session id, create a child session with
/// that parent, register the child, prompt it, and extract the text response.
let runSubagent (client: obj) (agent: string) (title: string) (prompt: string)
                (directory: string) (sessionID: string) (context: obj) : JS.Promise<string> =
    runSubagentCore client agent title prompt directory sessionID context false

/// Run a subagent and clean up the child session afterwards. Used for
/// short-lived analysis subagents such as the executor summarizer.
let runSubagentWithCleanup (client: obj) (agent: string) (title: string) (prompt: string)
                           (directory: string) (sessionID: string) (context: obj) : JS.Promise<string> =
    runSubagentCore client agent title prompt directory sessionID context true

/// Create a reviewer child session under the given parent, register it, and
/// return the child id (empty string on failure).
let createReviewerChild (client: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
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
            ChildAgent.registerChildAgent childID "reviewer" parentID
        return childID
    }
    |> Async.StartAsPromise

/// Run the reviewer prompt-nudge loop on an existing child session: prompt with
/// the review instructions, wait for the verdict via submit_review_result,
/// nudging up to maxNudges times if the reviewer hasn't submitted.
let runReviewerLoop (client: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
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
                            body = box {| agent = "reviewer"; parts = parts; tools = box {| submit_review_result = true |} |}
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
let runReviewerSession (client: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
                       (directory: string) (sessionID: string) (task: string)
                       : JS.Promise<VibeFs.Kernel.ReviewSession.ReviewResult> =
    async {
        let parentID = ChildAgent.resolveSubsessionParentID (if sessionID = "" then None else Some sessionID)
        let! childID = createReviewerChild client reviewStore directory parentID sessionID "Pre-Reviewer" |> Async.AwaitPromise
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
let runSubmitReview (client: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
                    (directory: string) (sessionID: string)
                    (report: string) (affectedFiles: string list)
                    (task: string) (abortSignal: obj)
                    : JS.Promise<VibeFs.Kernel.ReviewSession.ReviewResult> =
    async {
        let parentID = ChildAgent.resolveSubsessionParentID (Some sessionID)
        let! childID = createReviewerChild client reviewStore directory parentID sessionID "Reviewer" |> Async.AwaitPromise
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

