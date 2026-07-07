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
open Wanxiangshu.Shell.ReadDedupMuxPlugin
open Wanxiangshu.Mux.BacklogSession
open Wanxiangshu.Mux.CapsCodec
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.JsArrayMutate
open Wanxiangshu.Shell.MessageTransformCommon
open Wanxiangshu.Shell.MuxHookInputCodec
open Wanxiangshu.Shell.MuxWorkspaceCodec
open Wanxiangshu.Shell.ChatTransformOutputCodec
open Wanxiangshu.Shell.Dyn

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
                let excluded =
                    shouldExcludeAgentFromProjection agent (isChildWorkspace deps sessionID)
                let typedMessages = decodeMessages sessionID messagesArr
                let cleanedMessages = stripSyntheticBySource typedMessages
                let backlogOps =
                    backlogSessionOpsFrom backlogSession.Host
                        (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))
                let plan = {
                    SessionID = sessionID
                    Agent = agent
                    Directory = directory
                    Excluded = excluded
                    IsSubagentSession = isChildWorkspace deps sessionID
                    Cleaned = cleanedMessages
                    RawArray = Some messagesArr
                }
                let replayTexts () : JS.Promise<string seq> =
                    Promise.lift (extractTextsFromEncodedMessages messagesArr)
                let dedupFn excluded encoded =
                    if excluded then encoded else deduplicateReadOutputsWithSeenByPath Map.empty encoded
                let injectFn _ encoded = Promise.lift encoded
                let loadCaps () =
                    let parentSessionID =
                        match tryGetParentWorkspaceId deps sessionID with
                        | Some parentId -> parentId
                        | None -> sessionID
                    let planWithParent = { plan with SessionID = parentSessionID }
                    loadCapsForScope runtimeScope RequireDirectory planWithParent
                let buildCaps encoded capsFiles prelude = buildCapsMessages encoded capsFiles prelude
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
                        dedupFn
                        loadCaps
                        buildCaps
                if not cleanedMessages.IsEmpty then replaceArrayInPlace messagesArr final
    }