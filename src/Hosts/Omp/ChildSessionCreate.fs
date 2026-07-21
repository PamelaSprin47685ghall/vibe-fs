module Wanxiangshu.Hosts.Omp.ChildSessionCreate

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.PiResolve
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn

let callOpt (ctx: obj) (key: string) : obj =
    let g = Dyn.get ctx key
    if Dyn.typeIs g "function" then Dyn.call0 g else box null

let initializeChildSession (scope: RuntimeScope) (pi: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let! codingAgent = getCodingAgentModule scope
        let cwd = string (Dyn.get ctx "cwd")
        let sessionManagerType = Dyn.get codingAgent "SessionManager"
        return createSessionManager sessionManagerType cwd
    }

/// None → omit model (DelegateToHost). Some "" is treated as omit, never empty string key.
let resolveModel (modelOverride: string option) (ctx: obj) : obj option =
    match modelOverride with
    | Some m when m.Trim() <> "" -> Some(box m)
    | Some _ -> None
    | None ->
        let m = Dyn.get ctx "model"

        if Dyn.isNullish m then None
        elif Dyn.typeIs m "string" && string m = "" then None
        else Some m

let buildCustomTools (pi: obj) (toolNames: string array) (customTools: obj array) : obj array =
    let myCustomTools = ResizeArray<obj>()

    if not (Dyn.isNullish customTools) then
        for t in customTools do
            myCustomTools.Add(t)

    let registeredTools = Dyn.get pi "toolDefinitions"

    if not (Dyn.isNullish registeredTools) && Dyn.isArray registeredTools then
        let arr = unbox<obj array> registeredTools

        for t in arr do
            let name = Dyn.str t "name"

            if name <> "" && Array.contains name toolNames then
                myCustomTools.Add(t)

    myCustomTools.ToArray()

let buildSessionBody
    (ctx: obj)
    (toolNames: string array)
    (myCustomTools: obj array)
    (modelOverrideResolved: obj option)
    (systemPrompt: obj option)
    (sessionManager: obj)
    : obj =
    let cwd = string (Dyn.get ctx "cwd")

    let sp =
        match systemPrompt with
        | Some v -> v
        | None -> callOpt ctx "getSystemPrompt"

    let baseBody =
        createObj
            [ "cwd", box cwd
              "hasUI", box false
              "toolNames", box toolNames
              "modelRegistry", Dyn.get ctx "modelRegistry"
              "thinkingLevel", callOpt ctx "getThinkingLevel"
              "systemPrompt", sp
              "agentsMdSearch", Dyn.get ctx "agentsMdSearch"
              "workspaceTree", Dyn.get ctx "workspaceTree"
              "sessionManager", box sessionManager
              "customTools", box myCustomTools
              "authStorage", Dyn.get ctx "authStorage"
              "ui", Dyn.get ctx "ui" ]

    match modelOverrideResolved with
    | Some m -> Dyn.withKey baseBody "model" m
    | None -> baseBody

let parseFallbackConfig (fallbackConfigOpt: FallbackConfig option) : string option * FallbackChain option =
    match fallbackConfigOpt with
    | Some(cfg: FallbackConfig) ->
        let firstModel =
            match cfg.DefaultChain with
            | first :: _ ->
                Some(
                    sprintf
                        "%s/%s%s"
                        first.ProviderID
                        first.ModelID
                        (match first.Variant with
                         | Some v -> ":" + v
                         | None -> "")
                )
            | [] -> None

        firstModel, Some cfg.DefaultChain
    | None -> None, None

let getSessionId (ctx: obj) : string =
    let s = Dyn.get ctx "sessionId"
    if Dyn.isNullish s then "" else string s

let setupSubagentSession
    (scope: RuntimeScope)
    (sessionId: string)
    (childId: string)
    (session: obj)
    (prompt: string)
    (defaultChain: FallbackChain option)
    (fallbackRuntime: FallbackRuntimeStore)
    : unit =
    scope.Add("omp_session_" + childId, session)

    defaultChain
    |> Option.iter (fun chain -> fallbackRuntime.UpdateSession(childId, selectChain chain))
