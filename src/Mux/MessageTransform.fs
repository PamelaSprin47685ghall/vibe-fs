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
open Wanxiangshu.Shell.MuxHookInputCodec
open Wanxiangshu.Shell.MuxWorkspaceCodec
open Wanxiangshu.Shell.ChatTransformOutputCodec
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ContextBudgetUsageCodec

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

            let projectionPolicy =
                if shouldExcludeAgentFromProjection agent (isChildWorkspace deps sessionID) then
                    ProjectionPolicy.ExcludeProjection
                else
                    ProjectionPolicy.IncludeProjection

            let typedMessages = decodeMessages sessionID messagesArr
            let cleanedMessages = stripSyntheticBySource typedMessages

            let backlogOps =
                backlogSessionOpsFrom backlogSession.Host (fun sid msgs ->
                    backlogSession.GetOrRebuildBacklog(sid, msgs))

            let! maxInputTokens = resolveMaxInputTokens [ deps; input ] sessionID directory

            let getContextUsage =
                match ContextBudgetUsageCodec.tryGetRealContextUsage deps sessionID directory with
                | Some f -> f
                | None -> fun _ -> Promise.lift None

            let plan =
                { SessionID = sessionID
                  Agent = agent
                  Directory = directory
                  ProjectionPolicy = projectionPolicy
                  IsSubagentSession = isChildWorkspace deps sessionID
                  Cleaned = cleanedMessages
                  RawArray = Some messagesArr
                  SembleInjectEnabled = false
                  Scope = runtimeScope
                  MaxInputTokens = maxInputTokens
                  GetContextUsage = getContextUsage }

            let replayTexts () : JS.Promise<string seq> =
                Promise.lift (extractTextsFromEncodedMessages messagesArr)

            let injectFn _ encoded = Promise.lift encoded

            let loadCaps () =
                let parentSessionID =
                    match tryGetParentWorkspaceId deps sessionID with
                    | Some parentId -> parentId
                    | None -> sessionID

                let planWithParent =
                    { plan with
                        SessionID = parentSessionID }

                loadCapsForScope runtimeScope RequireDirectory planWithParent

            let buildCaps encoded capsFiles prelude =
                buildCapsMessages encoded capsFiles prelude

            let! final =
                runHostMessagesTransform
                    reviewStore
                    sessionID
                    IfStoreEmpty
                    replayTexts
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
            let cleaned = stripSyntheticBySource typedMessages
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

            do!
                Wanxiangshu.Shell.EventLogRuntime.appendCompactionStartedOrFail
                    directory
                    sessionID
                    compactionId
                    gen
                    turnId

            match fallbackRuntime with
            | Some fr ->
                fr.SetSessionOwner sessionID "Compaction"
                fr.SetActiveCompactionId(sessionID, compactionId)
            | None -> ()

            let result =
                Wanxiangshu.Kernel.BacklogProjectionCore.compactingTransform cleaned backlog guidGen

            let encoded = encodeMessages result
            let promptBody = box {| parts = [| box {| ``type`` = "text"; text = "​" |} |] |}

            output?context <- encoded
            output?prompt <- promptBody
    }
