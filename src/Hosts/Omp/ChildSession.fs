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
open Wanxiangshu.Runtime.Fallback.FallbackRecoveryWait
open Wanxiangshu.Runtime.SubagentIo
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Hosts.Omp.SubagentRuntime
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Omp.ChildSessionCreate

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



let createDispose
    (wrapper: IAgentSessionWrapper)
    (scope: RuntimeScope)
    (cwd: string)
    (childId: string)
    : (unit -> unit) option =
    match wrapper.dispose with
    | Some d ->
        let wrapped () =
            try
                d ()
            finally
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

let setupChildEnvironment
    (innerPi: obj)
    (body: obj)
    (sessionManager: obj)
    (scope: RuntimeScope)
    : JS.Promise<IAgentSessionWrapper * obj * string> =
    promise {
        let! wrapperObj = unbox<JS.Promise<obj>> (Dyn.call1 (Dyn.get innerPi "createAgentSession") (box body))
        let wrapper = unbox<IAgentSessionWrapper> wrapperObj
        let session = wrapper.session

        let childId =
            if Dyn.typeIs (Dyn.get sessionManager "getSessionId") "function" then
                let id: obj = sessionManager?getSessionId ()
                if Dyn.isNullish id then "" else string id
            else
                ""

        if childId <> "" then
            markChildSession scope childId

        return (wrapper, session, childId)
    }

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
        let cwd = string (Dyn.get ctx "cwd")

        if Dyn.isNullish innerPi || Dyn.isNullish (Dyn.get innerPi "createAgentSession") then
            return failwith "createAgentSession unavailable"
        else
            let! sessionManager = initializeChildSession scope pi ctx
            let modelOverrideResolved = resolveModel modelOverride ctx
            let myCustomTools = buildCustomTools pi toolNames customTools

            let body =
                buildSessionBody ctx toolNames myCustomTools modelOverrideResolved systemPrompt sessionManager

            body?customTools <- box myCustomTools
            let! (wrapper, session, childId) = setupChildEnvironment innerPi body sessionManager scope
            let dispose = createDispose wrapper scope cwd childId

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
        let (modelOverride, defaultChain) = parseFallbackConfig fallbackConfigOpt
        let sessionId = getSessionId ctx
        let! child = createChildSession scope pi ctx toolNames None [||] modelOverride
        let session = child.session
        let childId = child.childId

        if childId <> "" then
            setupSubagentSession scope sessionId childId session prompt defaultChain fallbackRuntime

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
