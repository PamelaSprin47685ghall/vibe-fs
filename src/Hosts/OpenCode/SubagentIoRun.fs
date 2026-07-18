module Wanxiangshu.Hosts.Opencode.SubagentIoRun

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.SubagentSpawn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.SessionIoSpawn
open Wanxiangshu.Runtime.SubsessionService
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter
open Wanxiangshu.Hosts.Opencode.SubagentIoCleanup
open Wanxiangshu.Hosts.Opencode.SubagentIoArgs

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.SubagentTypes

let formatRunFailure (f: RunFailure) : string = SubagentRunExec.formatRunFailure f

let executeSubagentRun
    (runtime: FallbackRuntimeStore)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionID: string)
    (childID: string)
    (prompt: string)
    (signal: obj)
    (cleanup: bool)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let! cfg, directive =
            SubagentRunDirective.extractRunDirective registry runtime client agent directory sessionID childID

        SubagentRunDirective.applyDirective runtime childID directive

        return!
            SubagentRunExec.runSubagentInternal
                registry
                runtime
                client
                agent
                directory
                sessionID
                childID
                prompt
                signal
                cleanup
                cfg
                directive
    }

let runSubagentCoreResult
    (runtime: FallbackRuntimeStore)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    (tools: obj)
    (cleanup: bool)
    (existingChildID: string option)
    : JS.Promise<Result<string, DomainError>> =
    promise {
        let signal = getAbortSignal context
        let options = buildSubagentOptions agent title prompt directory sessionID tools

        try
            let! childResult =
                SubagentRunExec.resolveOrCreateChild registry client sessionID agent existingChildID options

            match childResult with
            | Error err -> return Error err
            | Ok childID ->
                try
                    return!
                        SubagentRunExec.dispatchSubagentRun
                            executeSubagentRun
                            registry
                            runtime
                            client
                            agent
                            directory
                            sessionID
                            childID
                            prompt
                            signal
                            cleanup
                with err ->
                    return Error(translateJsError err)
        with err ->
            return Error(translateJsError err)
    }

let runSubagentWithCleanup
    (runtime: FallbackRuntimeStore)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult runtime registry client agent title prompt directory sessionID context (box null) true None

let resolveSubagentPromise (context: string) (work: JS.Promise<Result<string, DomainError>>) : JS.Promise<string> =
    promise {
        let! result = work

        return
            match result with
            | Ok text -> text
            | Error err -> wireEncodeToolError context err
    }

let runSubagent
    (runtime: FallbackRuntimeStore)
    (registry: ChildAgentRegistry)
    (client: obj)
    (agent: string)
    (title: string)
    (prompt: string)
    (directory: string)
    (sessionID: string)
    (context: obj)
    (tools: obj)
    : JS.Promise<Result<string, DomainError>> =
    runSubagentCoreResult runtime registry client agent title prompt directory sessionID context tools false None
