module VibeFs.Mux.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.CapsFormat
open VibeFs.Shell.MessageTransformCore
open VibeFs.Shell.MessageTransformHostEntry
open VibeFs.Shell.MessageTransformHostHooks
open VibeFs.Shell.MessageTransformPipeline
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.RuntimeState
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Methodology
open VibeFs.Kernel.MessageTransformPolicy
open VibeFs.Mux.KnowledgeGraphRuntimeMux
open VibeFs.Mux.KnowledgeGraphRuntimeMuxQuery
open VibeFs.Mux.MessagingCodec
open VibeFs.Shell.ReadDedupMuxPlugin
open VibeFs.Mux.BacklogSession
open VibeFs.Mux.CapsCodec
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.JsArrayMutate
open VibeFs.Shell.MessageTransformCommon
open VibeFs.Shell.MuxHookInputCodec
open VibeFs.Shell.MuxWorkspaceCodec
open VibeFs.Shell.ChatTransformOutputCodec

let messagesTransform
    (deps: obj)
    (runtimeScope: VibeFs.Shell.RuntimeScope.RuntimeScope)
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
                let replayTexts () = extractTextsFromEncodedMessages messagesArr
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
                        Always
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