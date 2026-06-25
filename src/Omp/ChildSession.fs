module VibeFs.Omp.ChildSession

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Omp.Codec
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.PiResolve
open VibeFs.Shell.Dyn
open VibeFs.Shell.OmpHostBindings
open VibeFs.Shell.SubagentIo
module Dyn = VibeFs.Shell.Dyn

type ChildSession = { session: obj; dispose: (unit -> unit) option }

/// Process-scoped registry of child session ids whose tool calls must not feed
/// the bookkeeper. Mirrors `Shell.ChildAgentRegistry` semantics but kept local
/// to Omp: `tool_result` only sees the host ctx, and matching the parent
/// session id against this set is enough to suppress child-agent noise without
/// requiring a full Kernel.Domain state machine on the hot path.
let mutable private childSessionIds : Set<string> = Set.empty

let markChildSession (id: string) =
    if id <> "" then childSessionIds <- Set.add id childSessionIds

let unmarkChildSession (id: string) =
    if id <> "" then childSessionIds <- Set.remove id childSessionIds

let isChildSession (id: string) : bool =
    id <> "" && Set.contains id childSessionIds

let clearChildSessionsForTest () : unit =
    childSessionIds <- Set.empty

let private callOpt (ctx: obj) (key: string) : obj =
    let g = Dyn.get ctx key
    if Dyn.typeIs g "function" then Dyn.call0 g else box null

let createChildSession (pi: obj) (ctx: obj) (toolNames: string array) (systemPrompt: obj option) (customTools: obj array)
    : JS.Promise<ChildSession> =
    promise {
        let createAgentSession = getCreateAgentSession pi
        if Dyn.isNullish createAgentSession || not (Dyn.typeIs createAgentSession "function") then
            return failwith "createAgentSession unavailable"
        let! codingAgent = getCodingAgentModule ()
        let sessionManagerType = Dyn.get codingAgent "SessionManager"
        let cwd = string (Dyn.get ctx "cwd")
        let sm = createSessionManager sessionManagerType cwd
        let sp =
            match systemPrompt with
            | Some v -> v
            | None -> callOpt ctx "getSystemPrompt"
        let body =
            createObj [
                "cwd", box cwd
                "hasUI", box false
                "toolNames", box toolNames
                "modelRegistry", Dyn.get ctx "modelRegistry"
                "model", Dyn.get ctx "model"
                "thinkingLevel", callOpt ctx "getThinkingLevel"
                "systemPrompt", sp
                "agentsMdSearch", Dyn.get ctx "agentsMdSearch"
                "workspaceTree", Dyn.get ctx "workspaceTree"
                "sessionManager", box sm
                "customTools", box customTools
            ]
        let! wrapper = unbox<JS.Promise<obj>> (Dyn.call1 createAgentSession (box body))
        let session = Dyn.get wrapper "session"
        let childId =
            let childCtx = createObj [ "sessionManager", Dyn.get session "sessionManager" ]
            getSessionIdFromContext childCtx |> Option.defaultValue ""
        if childId <> "" then markChildSession childId
        let dispose =
            let d = Dyn.get wrapper "dispose"
            if Dyn.typeIs d "function" then
                let wrapped () =
                    try
                        Dyn.call0 d |> ignore
                    finally
                        unmarkChildSession childId
                Some wrapped
            else None
        return { session = session; dispose = dispose }
    }

let runSubagent (pi: obj) (ctx: obj) (toolNames: string array) (prompt: string) (signal: obj option)
    : JS.Promise<string> =
    promise {
        let! child = createChildSession pi ctx toolNames None [||]
        let session = child.session
        let run =
            promise {
                do! sessionPrompt session prompt
                do! sessionWaitForIdle session
                let sm = Dyn.get session "sessionManager"
                return readAssistantText sm 0 "\n\n" |> Option.defaultValue noOutputText
            }
        let cleanup () =
            let abort = sessionAbort session
            if Dyn.typeIs abort "function" then Dyn.call0 abort |> ignore
            child.dispose |> Option.iter (fun dispose -> dispose ())
        let! text = raceWithAbortSignal (Option.defaultValue (box null) signal) cleanup run
        cleanup ()
        return text
    }
