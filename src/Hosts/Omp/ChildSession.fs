module Wanxiangshu.Hosts.Omp.ChildSession

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.PiResolve
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.FallbackRecoveryWait
open Wanxiangshu.Runtime.SubagentIo
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Hosts.Omp.SubagentRuntime
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn

type ChildSession =
    { session: obj
      childId: string
      dispose: (unit -> unit) option }

let getChildIds (scope: RuntimeScope) : Set<string> =
    match scope.TryFindKey "omp_child_sessions" with
    | Some v -> unbox v
    | None -> Set.empty

let setChildIds (scope: RuntimeScope) (ids: Set<string>) : unit =
    scope.Add("omp_child_sessions", box ids)

let markChildSession (scope: RuntimeScope) (id: string) =
    if id <> "" then
        setChildIds scope (Set.add id (getChildIds scope))

let unmarkChildSession (scope: RuntimeScope) (id: string) =
    if id <> "" then
        setChildIds scope (Set.remove id (getChildIds scope))

let isChildSession (scope: RuntimeScope) (id: string) : bool =
    id <> "" && Set.contains id (getChildIds scope)

let clearChildSessionsForTest (scope: RuntimeScope) () : unit = setChildIds scope Set.empty

let callOpt (ctx: obj) (key: string) : obj =
    let g = Dyn.get ctx key
    if Dyn.typeIs g "function" then Dyn.call0 g else box null

let createChildSession
    (scope: RuntimeScope)
    (pi: obj)
    (ctx: obj)
    (toolNames: string array)
    (systemPrompt: obj option)
    (customTools: obj array)
    (modelOverride: string option)
    : JS.Promise<ChildSession> =
    promise {
        let innerPi = Dyn.get pi "pi"

        if Dyn.isNullish innerPi || Dyn.isNullish (Dyn.get innerPi "createAgentSession") then
            return failwith "createAgentSession unavailable"
        else
            let createFn = Dyn.get innerPi "createAgentSession"
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

            let body =
                createObj
                    [ "cwd", box cwd
                      "hasUI", box false
                      "toolNames", box toolNames
                      "modelRegistry", Dyn.get ctx "modelRegistry"
                      "model", model
                      "thinkingLevel", callOpt ctx "getThinkingLevel"
                      "systemPrompt", sp
                      "agentsMdSearch", Dyn.get ctx "agentsMdSearch"
                      "workspaceTree", Dyn.get ctx "workspaceTree"
                      "sessionManager", box sm
                      "customTools", box (myCustomTools.ToArray())
                      "authStorage", Dyn.get ctx "authStorage"
                      "ui", Dyn.get ctx "ui" ]

            body?customTools <- box (myCustomTools.ToArray())

            let! wrapperObj = unbox<JS.Promise<obj>> (Dyn.call1 createFn (box body))
            let wrapper = unbox<IAgentSessionWrapper> wrapperObj
            let session = wrapper.session

            let childId =
                if Dyn.typeIs (Dyn.get sm "getSessionId") "function" then
                    let id: obj = sm?getSessionId ()
                    if Dyn.isNullish id then "" else string id
                else
                    ""

            if childId <> "" then
                markChildSession scope childId

            let dispose =
                match wrapper.dispose with
                | Some d ->
                    let wrapped () =
                        try
                            d ()
                        finally
                            // Physical session teardown: dispose actor registry entry.
                            if childId <> "" then
                                Wanxiangshu.Runtime.SubsessionActorRegistry.SubsessionActorRegistry.Remove cwd childId

                            unmarkChildSession scope childId

                    Some wrapped
                | None ->
                    if childId = "" then
                        None
                    else
                        Some(fun () ->
                            Wanxiangshu.Runtime.SubsessionActorRegistry.SubsessionActorRegistry.Remove cwd childId
                            unmarkChildSession scope childId)

            return
                { session = session
                  childId = childId
                  dispose = dispose }
    }

let runSubagentWithId
    (scope: RuntimeScope)
    (pi: obj)
    (ctx: obj)
    (toolNames: string array)
    (prompt: string)
    (signal: obj option)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    : JS.Promise<string * string> =
    promise {
        let modelOverride, defaultChain =
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

        let sessionId =
            let s = Dyn.get ctx "sessionId"
            if Dyn.isNullish s then "" else string s

        let! child = createChildSession scope pi ctx toolNames None [||] modelOverride
        let session = child.session
        let childId = child.childId

        if childId <> "" then
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

        // AbortSignal is bound inside SubsessionService → CancelRequested.
        // Parent waits for actor terminal; no outer race that returns early.
        let! text =
            runOmpSubagentCore
                fallbackRuntime
                fallbackConfigOpt
                childId
                session
                prompt
                SubagentResetPolicy.ResetToActive
                sessionId
                pi
                signal

        return (text, childId)
    }

let runSubagent
    (scope: RuntimeScope)
    (pi: obj)
    (ctx: obj)
    (toolNames: string array)
    (prompt: string)
    (signal: obj option)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    : JS.Promise<string> =
    promise {
        let! (text, _) = runSubagentWithId scope pi ctx toolNames prompt signal fallbackRuntime fallbackConfigOpt
        return text
    }
