module VibeFs.Opencode.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.ReviewReplayPolicy
open VibeFs.Kernel.BacklogProjectionCore
open VibeFs.Shell.MessageTransformCore
open VibeFs.Shell.ReadDedupOpenCode
open VibeFs.Kernel.MessageTransformPolicy
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.Config
open VibeFs.Kernel.Methodology
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.BacklogSession
open VibeFs.Opencode.MessagingCodec
open VibeFs.Opencode.CapsCodec
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.OpencodeHookInputCodec
open VibeFs.Shell.ChatTransformOutputCodec
open VibeFs.Shell.JsArrayMutate

let private extractSessionID (messages: Message<obj> list) : string =
    messages
    |> List.tryPick (fun m -> if m.info.sessionID <> "" then Some m.info.sessionID else None)
    |> Option.defaultValue ""

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (runtimeScope: VibeFs.Shell.RuntimeScope.RuntimeScope) (backlogSession: BacklogSession) (knowledgeGraphRuntime: KnowledgeGraphRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
                let messagesList = MessagingCodec.decodeMessages messagesArr
                let agent = resolveMessagesTransformAgent registry input messagesList "build"
                let sessionID = extractSessionID messagesList
                VibeFs.Shell.ReviewReplaySync.replayReviewIfStoreEmpty
                    reviewStore
                    sessionID
                    (messagesList |> Messaging.flatten |> textsFromFlatParts)
                let cleaned = Messaging.stripSyntheticBySource messagesList
                if cleaned.IsEmpty then ()
                else
                    let excluded = shouldExcludeAgentFromProjection agent false
                    let backlogOps =
                        backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))
                    let afterBacklog = applyBacklogProjection sessionID excluded backlogOps cleaned
                    let encoded = MessagingCodec.encodeMessages afterBacklog
                    if not excluded then deduplicateOpencodeReadPartsInPlace encoded
                    let! capsFiles =
                        if excluded then Promise.lift ([]: CapsFile list)
                        else CapsFileCache.getOrLoadCapsFilesForScope runtimeScope sessionID directory
                    let! knowledgeGraphPrelude =
                        if not excluded && canUse agent "knowledge_graph_fetch" then knowledgeGraphRuntime.BuildPreludeForSession(sessionID, directory)
                        else Promise.lift (None: string option)
                    let final =
                        buildCapsMessages
                            VibeFs.Shell.FileSys.sha256HexTruncated
                            encoded
                            directory
                            capsFiles
                            knowledgeGraphPrelude
                    replaceArrayInPlace messagesArr final
    }

let compactingHandlerFor (_host: Host) (backlogSession: BacklogSession) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
                let messagesList = MessagingCodec.decodeMessages messagesArr
                let sessionID =
                    let fromInput = sessionIdFromHookInput input ""
                    if fromInput <> "" then fromInput else extractSessionID messagesList
                let cleaned = Messaging.stripSyntheticBySource messagesList
                if cleaned.IsEmpty then ()
                else
                    let backlogOps =
                        backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))
                    let afterBacklog = applyBacklogProjection sessionID false backlogOps cleaned
                    let encoded = MessagingCodec.encodeMessages afterBacklog
                    replaceArrayInPlace messagesArr encoded
    }

let compactingHandler (backlogSession: BacklogSession) (input: obj) (output: obj) : JS.Promise<unit> =
    compactingHandlerFor opencode backlogSession input output

let systemTransform (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { clearSystemOutputLength output }