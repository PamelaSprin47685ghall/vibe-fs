module Wanxiangshu.Shell.SubagentIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn

/// Host-side AI model settings used by subagent prompt bodies.  Mirrors the
/// shape of `Mux.AiSettings.DelegatedAiSettings` so the shell layer can build
/// prompt bodies without taking a dependency on the Mux tree.
type SubagentAiSettings =
    { ModelString: string option
      ThinkingLevel: string option
      Variant: string option }

let emptySettings : SubagentAiSettings =
    { ModelString = None
      ThinkingLevel = None
      Variant = None }

/// The full tool-execution context, normalized for shell-layer consumers.
/// Both Opencode and Omp adapt their host-specific `context` object into this
/// shape so downstream code (subagent launch, abort race, etc.) can stay
/// host-agnostic.
type ToolContext =
    { Directory: string
      SessionID: string
      AbortSignal: obj }

/// Look up a string field across multiple possible names. Used when host
/// `context` shapes drift (e.g. opencode uses `sessionID`, Omp's pi may use
/// `sessionId`).
let firstString (ctx: obj) (keys: string list) : string option =
    keys
    |> List.tryPick (fun key ->
        let v = Dyn.get ctx key
        if Dyn.isNullish v then None else Some (string v))

/// Get the abort signal from a host `context`.  Opencode exposes it as
/// `context.abort`; callers fall through to null on hosts that don't.
let getAbortSignal (context: obj) : obj =
    if Dyn.isNullish context then null
    else
        let abort = Dyn.get context "abort"
        if Dyn.isNullish abort then null else abort

/// Extract a normalized `ToolContext` from a host tool `context`.  When the
/// host does not provide a value, `SessionID` is null so the parent resolution
/// treats the call as top-level; `AbortSignal` is null when the host does not
/// expose one.
let extractToolContext (context: obj) (pluginDirectory: string) : ToolContext =
    let directory =
        match firstString context [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ] with
        | Some s when s <> "" -> s
        | _ -> pluginDirectory
    let sessionID =
        match firstString context [ "sessionID"; "sessionId"; "session_id" ] with
        | Some s when s <> "" -> s
        | _ -> ""
    { Directory = directory
      SessionID = sessionID
      AbortSignal = getAbortSignal context }

/// Dynamically invoke a method on `target`, awaiting the resulting promise.
let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let noOutputText = "(no output)"
let abortedPrefix = "(aborted)"

/// Placeholder for a subagent session that produced no assistant text.
let noOutputMessage () : string = noOutputText

/// Render the prefix attached to a session whose prompt was aborted.
let abortedPrefixMessage () : string = abortedPrefix

/// Format a single text part for the host's wire format.
let textPart (text: string) : obj = box (createObj [ "type", box "text"; "text", box text ])

/// Format a list of strings as a `parts` array for the host's wire format.
let textParts (parts: string list) : obj array =
    parts |> List.map textPart |> List.toArray

let private tryReadPromptModel (payload: obj) : obj option =
    let promptModel = Dyn.get payload "model"
    if not (Dyn.isNullish promptModel) then Some promptModel
    else
        let modelString = Dyn.str payload "modelString"
        if modelString = "" then None
        else
            let slash = modelString.IndexOf('/')
            if slash <= 0 || slash >= modelString.Length - 1 then None
            else Some (box {| providerID = modelString.[0..slash-1]; modelID = modelString.[slash+1..] |})

let buildPromptBody (agent: string) (prompt: string) (tools: obj) (settings: SubagentAiSettings) : obj =
    let body = box {| agent = agent; parts = [| box {| ``type`` = "text"; text = prompt |} |] |}
    let body = if Dyn.isNullish tools then body else Dyn.withKey body "tools" tools
    let body =
        match settings.ModelString with
        | None -> body
        | Some modelString ->
            match tryReadPromptModel (createObj [ "modelString", box modelString ]) with
            | Some model -> Dyn.withKey body "model" model
            | None -> body
    let body =
        match settings.ThinkingLevel with
        | Some level when level.Trim() <> "" -> Dyn.withKey body "variant" (box level)
        | _ -> body
    body

/// True iff the AbortSignal has already fired.
let signalAborted (signal: obj) : bool =
    not (Dyn.isNullish signal) && Dyn.truthy (Dyn.get signal "aborted")

/// Wire an abort event on signal so a Promise.race can use it as a
/// cancellation source. Calls fire() (best-effort cleanup) and rejects.
let makeAbortPromise (signal: obj) (fire: unit -> unit) : JS.Promise<unit> =
    if Dyn.isNullish signal then
        unbox<JS.Promise<unit>> (emitJsExpr () "Promise.resolve()")
    else
        let rejecter =
            emitJsExpr ()
                "Object.assign(new Error('Aborted'), { name: 'AbortError' })"
        Promise.create (fun _resolve reject ->
            let handler () =
                try
                    try fire () with _ -> ()
                    reject rejecter
                with _ -> ()
            if signalAborted signal then
                try fire () with _ -> ()
                reject rejecter
            else
                try signal?addEventListener("abort", box handler)
                with _ -> ())

/// Race a work promise against an AbortSignal.  When the signal fires, the
/// onAbort callback is invoked (caller uses it to clean up child sessions
/// etc.) and the returned promise rejects with an `AbortError`.
let raceWithAbortSignal (signal: obj) (onAbort: unit -> unit) (work: JS.Promise<'T>) : JS.Promise<'T> =
    if Dyn.isNullish signal then work
    else
        let abortP = makeAbortPromise signal onAbort
        // The race rejects as soon as abortP rejects; lift to 'T so the list
        // element type matches work.
        let abortAsT = unbox<JS.Promise<'T>> (box abortP)
        Promise.race [ work; abortAsT ]
