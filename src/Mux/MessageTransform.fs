module Wanxiangshu.Mux.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.MessageTransformHostHooks
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Mux.MessagingCodec
open Wanxiangshu.Mux.BacklogSession
open Wanxiangshu.Mux.CapsCodec
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.JsArrayMutate
open Wanxiangshu.Shell.MessageTransformCommon
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.MuxHookInputCodec
open Wanxiangshu.Shell.MuxWorkspaceCodec
open Wanxiangshu.Shell.ChatTransformOutputCodec
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ContextBudgetUsageCodec

let private maxInputTokensCache =
    System.Collections.Generic.Dictionary<string, int>()

let messagesTransform
    (deps: obj)
    (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: ReviewStore)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxMessagesTransformInput input deps
        let directory = decoded.Directory
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
            let agent = decoded.Agent
            let sessionID = decoded.SessionID

            let isChild = isChildWorkspace deps sessionID

            let backlogPolicy =
                Wanxiangshu.Kernel.MessageTransformPolicy.getBacklogProjectionPolicy agent isChild

            let capsPolicy =
                Wanxiangshu.Kernel.MessageTransformPolicy.getCapsInjectionPolicy agent isChild

            let parallelHintPolicy =
                Wanxiangshu.Kernel.MessageTransformPolicy.getParallelHintPolicy agent isChild

            let contextBudgetPolicy =
                Wanxiangshu.Kernel.MessageTransformPolicy.getContextBudgetPolicy agent isChild

            let typedMessages = decodeMessages sessionID messagesArr
            let cleanedMessages = typedMessages

            let backlogOps =
                backlogSessionOpsFrom backlogSession.Host (fun sid msgs ->
                    backlogSession.GetOrRebuildBacklog(sid, msgs))

            let! maxInputTokens =
                match maxInputTokensCache.TryGetValue(sessionID) with
                | true, limit -> Promise.lift limit
                | _ ->
                    promise {
                        let! limit = resolveMaxInputTokens [ deps; input ] sessionID directory
                        maxInputTokensCache.[sessionID] <- limit
                        return limit
                    }

            let getContextUsage =
                match ContextBudgetUsageCodec.tryGetRealContextUsage deps sessionID directory with
                | Some f -> f
                | None -> fun _ -> Promise.lift None

            let plan =
                { SessionID = sessionID
                  Agent = agent
                  Directory = directory
                  ProjectionPolicy =
                    (if backlogPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include then
                         ProjectionPolicy.IncludeProjection
                     else
                         ProjectionPolicy.ExcludeProjection)
                  BacklogProjectionPolicy = backlogPolicy
                  CapsInjectionPolicy = capsPolicy
                  ParallelHintPolicy = parallelHintPolicy
                  ContextBudgetPolicy = contextBudgetPolicy
                  IsSubagentSession = isChild
                  Cleaned = cleanedMessages
                  RawArray = Some messagesArr
                  SembleInjectEnabled = false
                  Scope = runtimeScope
                  MaxInputTokens = maxInputTokens
                  GetContextUsage = getContextUsage }

            let injectFn _ encoded = Promise.lift encoded

            let loadCaps () =
                promise {
                    let parentSessionID =
                        match tryGetParentWorkspaceId deps sessionID with
                        | Some parentId -> parentId
                        | None -> sessionID

                    let planWithParent =
                        { plan with
                            SessionID = parentSessionID }

                    let! caps = loadCapsForScope runtimeScope RequireDirectory planWithParent
                    return caps |> List.sortBy (fun cf -> cf.label, cf.filePath)
                }

            let buildCaps encoded capsFiles prelude =
                buildCapsMessages sessionID encoded capsFiles prelude

            let! final =
                runHostMessagesTransform
                    reviewStore
                    sessionID
                    plan
                    backlogOps
                    encodeMessages
                    injectFn
                    loadCaps
                    buildCaps

            if not cleanedMessages.IsEmpty then
                replaceArrayInPlace messagesArr final
    }

let compactingTransform
    (deps: obj)
    (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxMessagesTransformInput input deps
        let directory = decoded.Directory
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        let messagesArr =
            let fromInput = Dyn.get input "messages"

            if not (Dyn.isNullish fromInput) && Dyn.isArray fromInput then
                fromInput :?> obj array
            else
                let fromOutput = Dyn.get output "messages"

                if not (Dyn.isNullish fromOutput) && Dyn.isArray fromOutput then
                    fromOutput :?> obj array
                else
                    [||]

        if messagesArr.Length > 0 then
            let sessionID = decoded.SessionID
            let typedMessages = decodeMessages sessionID messagesArr
            let cleaned = typedMessages
            let backlog = backlogSession.GetOrRebuildBacklog(sessionID, cleaned)

            let guidGen () =
                let rg = get deps "RandomGen"

                if not (isNullish rg) then
                    string (rg $ ())
                else
                    System.Guid.NewGuid().ToString()

            let fallbackRuntime =
                match runtimeScope.TryFindKey("fallbackRuntime") with
                | Some obj -> Some(unbox<Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState> obj)
                | None -> None

            let compactionId = "compact-" + System.Guid.NewGuid().ToString("N")

            let gen =
                match fallbackRuntime with
                | Some fr -> fr.GetSessionGeneration sessionID
                | None -> 0

            let turnId =
                match fallbackRuntime with
                | Some fr -> fr.GetHumanTurnId sessionID
                | None -> ""

            let compactionOrdinal =
                match fallbackRuntime with
                | Some fr -> fr.IncrementCompactionOrdinal sessionID
                | None -> 0

            do!
                Wanxiangshu.Shell.EventLogRuntime.appendCompactionStartedOrFail
                    directory
                    sessionID
                    compactionId
                    gen
                    turnId
                    compactionOrdinal

            match fallbackRuntime with
            | Some fr ->
                fr.SetSessionOwner sessionID SessionOwner.Compaction
                fr.SetActiveCompactionId(sessionID, compactionId, compactionOrdinal)
            | None -> ()

            let result =
                Wanxiangshu.Kernel.BacklogProjectionCore.compactingTransform cleaned backlog guidGen

            let encoded = encodeMessages result
            let promptBody = box {| parts = [| box {| ``type`` = "text"; text = "​" |} |] |}

            output?context <- encoded
            output?prompt <- promptBody
    }
