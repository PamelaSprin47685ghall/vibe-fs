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
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Mux.KnowledgeGraphRuntimeMux
open Wanxiangshu.Mux.KnowledgeGraphRuntimeMuxQuery
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

let messagesTransform
    (deps: obj)
    (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (reviewStore: ReviewStore)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
                let decoded = decodeMuxMessagesTransformInput input deps
                let agent = decoded.Agent
                let sessionID = decoded.SessionID
                let directory = decoded.Directory
                let excluded =
                    shouldExcludeAgentFromProjection agent (isChildWorkspace deps sessionID)
                let typedMessages = decodeMessages sessionID messagesArr
                let cleanedMessages = stripSyntheticBySource typedMessages
                let backlogOps =
                    backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))
                let plan = {
                    SessionID = sessionID
                    Agent = agent
                    Directory = directory
                    Excluded = excluded
                    Cleaned = cleanedMessages
                }
                let replayTexts () : JS.Promise<string seq> =
                    Promise.lift (extractTextsFromEncodedMessages messagesArr)
                let dedupFn excluded encoded =
                    if excluded then encoded else deduplicateReadOutputsWithSeenByPath Map.empty encoded
                let loadCaps () =
                    loadCapsForScope runtimeScope RequireDirectory plan
                let loadKgPrelude () =
                    loadKgPreludeForAgent true agent plan (fun sid dir -> knowledgeGraphRuntime.BuildPreludeForSession(sid, dir))
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
                        dedupFn
                        loadCaps
                        loadKgPrelude
                        buildCaps
                if not cleanedMessages.IsEmpty then replaceArrayInPlace messagesArr final
    }

let compactingTransform (deps: obj) (backlogSession: BacklogSession) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
            let decoded = decodeMuxMessagesTransformInput input deps
            let sessionID = decoded.SessionID
            let typedMessages = decodeMessages sessionID messagesArr
            let cleaned = stripSyntheticBySource typedMessages
            if cleaned.IsEmpty then ()
            else
                let backlogOps =
                    backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))
                let afterBacklog = applyBacklogProjection sessionID false backlogOps cleaned
                let encoded = encodeMessages afterBacklog
                replaceArrayInPlace messagesArr encoded
    }