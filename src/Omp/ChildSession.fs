module VibeFs.Omp.ChildSession

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Omp.Codec
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.PiResolve
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn

type ChildSession = { session: obj; dispose: (unit -> unit) option }

let private callOpt (ctx: obj) (key: string) : obj =
    let g = Dyn.get ctx key
    if Dyn.typeIs g "function" then Dyn.call0 g else box null

let createChildSession (pi: obj) (ctx: obj) (toolNames: string array) (systemPrompt: obj option) (customTools: obj array)
    : JS.Promise<ChildSession> =
    promise {
        let createAgentSession = pi?pi?createAgentSession
        if Dyn.isNullish createAgentSession || not (Dyn.typeIs createAgentSession "function") then
            return failwith "createAgentSession unavailable"
        let! codingAgent = getCodingAgentModule ()
        let sessionManagerType = Dyn.get codingAgent "SessionManager"
        let cwd = string (Dyn.get ctx "cwd")
        let sm = sessionManagerType?create(cwd)
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
        let! wrapper = createAgentSession(body)
        let session = Dyn.get wrapper "session"
        let dispose =
            let d = Dyn.get wrapper "dispose"
            if Dyn.typeIs d "function" then Some (fun () -> Dyn.call0 d |> ignore) else None
        return { session = session; dispose = dispose }
    }

let runSubagent (pi: obj) (ctx: obj) (toolNames: string array) (prompt: string) (signal: obj option)
    : JS.Promise<string> =
    promise {
        let! child = createChildSession pi ctx toolNames None [||]
        let session = child.session
        let promptFn = session?prompt
        let waitFn = session?waitForIdle
        let run =
            promise {
                do! promptFn(prompt)
                do! waitFn()
                let sm = Dyn.get session "sessionManager"
                return readAssistantText sm 0 "\n\n" |> Option.defaultValue "(no output)"
            }
        let abortErr = createAbortError ()
        let rejectAbort () = emitJsExpr abortErr "Promise.reject($0)" |> unbox<JS.Promise<string>>
        let! text =
            match signal with
            | None -> run
            | Some s when Dyn.truthy (Dyn.get s "aborted") -> rejectAbort ()
            | Some s ->
                let abortP =
                    Promise.create (fun _resolve reject ->
                        s?addEventListener(
                            "abort",
                            (fun _ -> reject (System.Exception "Aborted")),
                            createObj [ "once", box true ]))
                Promise.race [ run; abortP ]
        let abort = Dyn.get session "abort"
        if Dyn.typeIs abort "function" then Dyn.call0 abort |> ignore
        child.dispose |> Option.iter (fun dispose -> dispose ())
        return text
    }