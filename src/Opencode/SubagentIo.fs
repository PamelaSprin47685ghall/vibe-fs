module Wanxiangshu.Opencode.SubagentIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackRecoveryWait
open Wanxiangshu.Shell.DelegatedAiSettings
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.SessionIoSpawn
open Wanxiangshu.Opencode.SubagentSpawn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Opencode.SubagentTypes

let runSubagentCoreResult
    (runtime: FallbackRuntimeState)
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

        let options =
            { agent = agent
              title = title
              prompt = prompt
              directory = directory
              sessionID = sessionID
              tools = tools
              aiSettings =
                { modelString = None
                  thinkingLevel = None } }

        try
            let! childResult =
                match existingChildID with
                | Some cid ->
                    let parentID =
                        registry.ResolveSubsessionParentID(if sessionID = "" then None else Some sessionID)

                    registry.RegisterChildAgent(cid, agent, parentID)
                    Promise.lift (Ok cid)
                | None -> startSubagentSession registry client options

            match childResult with
            | Error err -> return Error err
            | Ok childID ->
                let abortAndUnregister () =
                    match getSessionApiFromClient client with
                    | Ok session ->
                        let abortPromise: JS.Promise<obj> =
                            invoke1 (box {| path = box {| id = childID |} |}) "abort" session

                        abortPromise |> ignore
                    | Error _ -> ()

                    registry.UnregisterChildAgent(childID)

                let cleanupChildIfRequested () =
                    if cleanup then
                        abortAndUnregister ()

                try
                    // Reset TaskComplete ONLY for new spawn (existingChildID is None).
                    // For continue (existingChildID is Some), TaskComplete will be set to false when the subsession
                    // receives a new tool/nudge cycle.
                    // Note: We intentionally do NOT clear Consumed or Retrying phase at the start of a continue call
                    // because they represent the ongoing fallback state chain. Once the new run completes and triggers
                    // a new task_complete event hook, it will update the state to Phase = Idle and TaskComplete = true,
                    // cleanly overriding the residual Retrying phase.
                    let initSt = runtime.GetOrCreateState childID

                    if existingChildID.IsNone then
                        runtime.UpdateState childID { initSt with TaskComplete = false }

                    runtime.SetSubsessionPending childID true
                    do! promptWithAbort client (buildPromptBody options childID) signal

                    do! waitForSubagentSettle runtime childID
                    runtime.ClearSubsessionPending childID

                    try
                        let st = runtime.GetOrCreateState childID

                        if st.Cancelled then
                            return Ok abortedPrefix
                        elif not st.TaskComplete && st.Phase <> FallbackPhase.Exhausted then
                            do! waitForSubagentSettle runtime childID
                            let st2 = runtime.GetOrCreateState childID

                            if st2.Cancelled then
                                return Ok abortedPrefix
                            else
                                let! text = extractSessionText client childID directory
                                return Ok(formatSubagentReport noOutputText abortedPrefix text false)
                        else
                            let! text = extractSessionText client childID directory
                            return Ok(formatSubagentReport noOutputText abortedPrefix text false)
                    finally
                        cleanupChildIfRequested ()
                with err ->
                    match translateJsError err with
                    | MessageAborted
                    | ClientCancellation _ ->
                        runtime.ClearSubsessionPending childID
                        abortAndUnregister ()

                        if not (Dyn.isNullish signal) && Dyn.truthy (Dyn.get signal "aborted") then
                            return Ok abortedPrefix
                        else
                            let! text = extractSessionText client childID directory
                            return Ok(formatSubagentReport noOutputText abortedPrefix text true)
                    | other ->
                        do! waitForSubagentSettle runtime childID
                        runtime.ClearSubsessionPending childID

                        let st = runtime.GetOrCreateState childID

                        let isSuccess = st.TaskComplete && not st.Cancelled

                        if isSuccess then
                            let! text = extractSessionText client childID directory
                            return Ok(formatSubagentReport noOutputText abortedPrefix text false)
                        else
                            return Error other
        with err ->
            return Error(translateJsError err)
    }

let runSubagentWithCleanup
    (runtime: FallbackRuntimeState)
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

let runSubagent
    (runtime: FallbackRuntimeState)
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
