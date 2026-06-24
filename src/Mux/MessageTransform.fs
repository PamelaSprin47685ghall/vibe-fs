module VibeFs.Mux.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.Config
open VibeFs.Kernel.CapsFormat
open VibeFs.Shell.MessageTransformCore
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Methodology
open VibeFs.Kernel.MessageTransformPolicy
open VibeFs.Mux.KnowledgeGraphTools
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
                VibeFs.Shell.ReviewReplaySync.replayReviewAlwaysSync reviewStore sessionID (extractTextsFromEncodedMessages messagesArr)
                let excluded =
                    shouldExcludeAgentFromProjection agent (isChildWorkspace deps sessionID)
                let typedMessages = decodeMessages sessionID messagesArr
                let cleanedMessages = stripSyntheticBySource typedMessages
                let backlogOps =
                    backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))
                let afterBacklog =
                    applyBacklogProjection sessionID excluded backlogOps cleanedMessages
                let encoded = encodeMessages afterBacklog
                let deduped = if excluded then encoded else deduplicateReadOutputsWithSeenByPath Map.empty encoded
                let! capsFiles =
                    if excluded || directory = "" then Promise.lift ([]: CapsFile list)
                    else CapsFileCache.getOrLoadCapsFilesForScope runtimeScope sessionID directory
                let! knowledgeGraphPrelude =
                    if not excluded && directory <> "" && canUse agent "knowledge_graph_fetch" then
                        knowledgeGraphRuntime.BuildPreludeForSession(sessionID, directory)
                    else
                        Promise.lift (None: string option)
                let final = buildCapsMessages deduped capsFiles knowledgeGraphPrelude
                replaceArrayInPlace messagesArr final
    }