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
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.PromptFrontMatter

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

let resolveModel (modelOverride: string option) (ctx: obj) : obj =
    match modelOverride with
    | Some m -> box m
    | None -> Dyn.get ctx "model"

let buildCustomTools (pi: obj) (toolNames: string array) (customTools: obj array) : obj array =
    let myCustomTools = ResizeArray<obj>()

    if not (Dyn.isNullish customTools) then
        for t in customTools do
            myCustomTools.Add(t)

    if Array.contains "return_reviewer" toolNames then
        let parentExtension = Dyn.get pi "extension"

        if not (Dyn.isNullish parentExtension) then
            let parentTools = Dyn.get parentExtension "tools"

            if not (Dyn.isNullish parentTools) then
                let entry = Dyn.callMethod1 parentTools "get" "return_reviewer"

                if not (Dyn.isNullish entry) then
                    let def = Dyn.get entry "definition"

                    if not (Dyn.isNullish def) then
                        myCustomTools.Add(def)

    myCustomTools.ToArray()

let buildSessionBody
    (ctx: obj)
    (toolNames: string array)
    (myCustomTools: obj array)
    (modelOverrideResolved: obj)
    (systemPrompt: obj option)
    (sessionManager: obj)
    : obj =
    let cwd = string (Dyn.get ctx "cwd")

    let sp =
        match systemPrompt with
        | Some v -> v
        | None -> callOpt ctx "getSystemPrompt"

    createObj
        [ "cwd", box cwd
          "hasUI", box false
          "toolNames", box toolNames
          "modelRegistry", Dyn.get ctx "modelRegistry"
          "model", modelOverrideResolved
          "thinkingLevel", callOpt ctx "getThinkingLevel"
          "systemPrompt", sp
          "agentsMdSearch", Dyn.get ctx "agentsMdSearch"
          "workspaceTree", Dyn.get ctx "workspaceTree"
          "sessionManager", box sessionManager
          "customTools", box myCustomTools
          "authStorage", Dyn.get ctx "authStorage"
          "ui", Dyn.get ctx "ui" ]

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
    let scalars = Wanxiangshu.Runtime.PromptFrontMatter.parseFrontMatterScalars prompt

    match Map.tryFind "objective" scalars with
    | Some objVal when not (System.String.IsNullOrWhiteSpace objVal) ->
        let parentKey = sessionId + "\u0000" + objVal.Trim()

        match scope.TryGetTempFiles(parentKey) with
        | Some files -> scope.RegisterTempFiles(childId + "\u0000" + objVal.Trim(), files)
        | None -> ()
    | _ -> ()

    defaultChain
    |> Option.iter (fun chain -> fallbackRuntime.SetChain childId chain)
