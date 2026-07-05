module Wanxiangshu.Omp.ChildSession

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.PiResolve
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.OmpHostBindings
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.SubagentIo
module Dyn = Wanxiangshu.Shell.Dyn

type ChildSession = { session: obj; dispose: (unit -> unit) option }


let private getChildIds (scope: RuntimeScope) : Set<string> =
    match scope.TryFindKey "omp_child_sessions" with
    | Some v -> unbox<Set<string>> v
    | None -> Set.empty

let private setChildIds (scope: RuntimeScope) (ids: Set<string>) : unit =
    scope.Add("omp_child_sessions", box ids)

let markChildSession (scope: RuntimeScope) (id: string) =
    if id <> "" then
        let ids = getChildIds scope
        setChildIds scope (Set.add id ids)

let unmarkChildSession (scope: RuntimeScope) (id: string) =
    if id <> "" then
        let ids = getChildIds scope
        setChildIds scope (Set.remove id ids)

let isChildSession (scope: RuntimeScope) (id: string) : bool =
    if id = "" then false else
    let ids = getChildIds scope
    Set.contains id ids

let clearChildSessionsForTest (scope: RuntimeScope) () : unit =
    setChildIds scope Set.empty<string>

let private callOpt (ctx: obj) (key: string) : obj =
    let g = Dyn.get ctx key
    if Dyn.typeIs g "function" then Dyn.call0 g else box null

let createChildSession (scope: RuntimeScope) (pi: obj) (ctx: obj) (toolNames: string array) (systemPrompt: obj option) (customTools: obj array) (modelOverride: string option)
    : JS.Promise<ChildSession> =
    promise {
        let piTyped = unbox<IPi> pi
        let createAgentSessionOpt =
            match piTyped.pi with
            | Some inner -> inner.createAgentSession
            | None -> None
        match createAgentSessionOpt with
        | None -> return failwith "createAgentSession unavailable"
        | Some createFn ->
            let! codingAgent = getCodingAgentModule scope
            let cwd = string (Dyn.get ctx "cwd")
            let sessionManagerType = Dyn.get codingAgent "SessionManager"
            let sm = createSessionManager sessionManagerType cwd
            let sp =
                match systemPrompt with
                | Some v -> v
                | None -> callOpt ctx "getSystemPrompt"
            let model =
                match modelOverride with
                | Some m -> box m
                | None -> Dyn.get ctx "model"
            let body =
                createObj [
                    "cwd", box cwd
                    "hasUI", box false
                    "toolNames", box toolNames
                    "modelRegistry", Dyn.get ctx "modelRegistry"
                    "model", model
                    "thinkingLevel", callOpt ctx "getThinkingLevel"
                    "systemPrompt", sp
                    "agentsMdSearch", Dyn.get ctx "agentsMdSearch"
                    "workspaceTree", Dyn.get ctx "workspaceTree"
                    "sessionManager", box sm
                    "customTools", box customTools
                    "authStorage", Dyn.get ctx "authStorage"
                    "ui", Dyn.get ctx "ui"
                ]
            let! wrapperObj = createFn (box body)
            let wrapper = unbox<IAgentSessionWrapper> wrapperObj
            let session = wrapper.session
            let childId =
                let childCtx = createObj [ "sessionManager", Dyn.get session "sessionManager" ]
                getSessionIdFromContext childCtx |> Option.defaultValue ""
            if childId <> "" then markChildSession scope childId
            let dispose =
                match wrapper.dispose with
                | Some d ->
                    let wrapped () =
                        try d ()
                        finally unmarkChildSession scope childId
                    Some wrapped
                | None -> None
            return { session = session; dispose = dispose }
    }

let runSubagent (scope: RuntimeScope) (pi: obj) (ctx: obj) (toolNames: string array) (prompt: string) (signal: obj option) (fallbackRuntime: Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState) (fallbackConfigOpt: Wanxiangshu.Kernel.FallbackKernel.Types.FallbackConfig option)
    : JS.Promise<string> =
    promise {
        let modelOverride, defaultChain =
            match fallbackConfigOpt with
            | Some cfg ->
                let firstModel =
                    match cfg.DefaultChain with
                    | first :: _ -> Some (sprintf "%s/%s%s" first.ProviderID first.ModelID (match first.Variant with Some v -> ":" + v | None -> ""))
                    | [] -> None
                firstModel, Some cfg.DefaultChain
            | None -> None, None
        let sessionId =
            let s = Dyn.get ctx "sessionId"
            if Dyn.isNullish s then "" else string s
        let! child = createChildSession scope pi ctx toolNames None [||] modelOverride
        let session = child.session
        let childId =
            let childCtx = createObj [ "sessionManager", Dyn.get session "sessionManager" ]
            getSessionIdFromContext childCtx |> Option.defaultValue ""
        if childId <> "" then
            let scalars = Wanxiangshu.Kernel.PromptFrontMatter.parseFrontMatterScalars prompt
            match Map.tryFind "objective" scalars with
            | Some objVal when not (System.String.IsNullOrWhiteSpace objVal) ->
                let parentKey = sessionId + "\u0000" + objVal.Trim()
                match scope.TryGetTempFiles(parentKey) with
                | Some files -> scope.RegisterTempFiles(childId + "\u0000" + objVal.Trim(), files)
                | None -> ()
            | _ -> ()
            defaultChain |> Option.iter (fun chain -> fallbackRuntime.SetChain childId chain)
        let run =
            promise {
                do! sessionPrompt session prompt
                do! sessionWaitForIdle session
                let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")
                return readAssistantText sm 0 "\n\n" |> Option.defaultValue noOutputText
            }
        let cleanup () =
            let childSess = unbox<IChildSession> session
            match childSess.abort with
            | Some _ -> try Dyn.callMethod0 session "abort" |> ignore with _ -> ()
            | None -> ()
            child.dispose |> Option.iter (fun dispose -> dispose ())
        let! text = raceWithAbortSignal (Option.defaultValue (box null) signal) cleanup run
        cleanup ()
        if childId <> "" && fallbackRuntime.GetConsumed childId <> Some false then
            let pst = fallbackRuntime.GetOrCreateState childId
            if pst.Phase = Wanxiangshu.Kernel.FallbackKernel.Types.FallbackPhase.Exhausted then
                return failwith "Fallback exhausted for child session"
        return text
    }
