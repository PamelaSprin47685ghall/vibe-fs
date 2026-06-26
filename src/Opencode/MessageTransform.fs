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
open VibeFs.Shell.MessageTransformHostEntry
open VibeFs.Shell.MessageTransformHostHooks
open VibeFs.Shell.MessageTransformPipeline
open VibeFs.Shell.ReadDedupOpenCode
open VibeFs.Kernel.MessageTransformPolicy
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.Methodology
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.BacklogSession
open VibeFs.Opencode.MessagingCodec
open VibeFs.Opencode.CapsCodec
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Opencode.SessionIo
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.OpencodeHookInputCodec
open VibeFs.Shell.ChatTransformOutputCodec
open VibeFs.Shell.JsArrayMutate

let private extractSessionID (messages: Message<obj> list) : string =
    messages
    |> List.tryPick (fun m -> if m.info.sessionID <> "" then Some m.info.sessionID else None)
    |> Option.defaultValue ""

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (runtimeScope: VibeFs.Shell.RuntimeScope.RuntimeScope) (backlogSession: BacklogSession) (knowledgeGraphRuntime: KnowledgeGraphRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (client: obj) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
                let messagesList = MessagingCodec.decodeMessages messagesArr
                let agent = resolveMessagesTransformAgent registry input messagesList "build"
                let sessionID = extractSessionID messagesList
                let cleaned = Messaging.stripSyntheticBySource messagesList
                let excluded = shouldExcludeAgentFromProjection agent false
                let backlogOps =
                    backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))
                let plan = {
                    SessionID = sessionID
                    Agent = agent
                    Directory = directory
                    Excluded = excluded
                    Cleaned = cleaned
                }
                let replayTexts () : JS.Promise<string seq> =
                    promise {
                        let! texts = readSessionTexts client sessionID directory
                        return texts :> string seq
                    }
                let dedupFn excluded encoded =
                    if excluded then encoded
                    else
                        deduplicateOpencodeReadPartsInPlace encoded
                        encoded
                let loadCaps () =
                    loadCapsForScope runtimeScope AllowEmptyDirectory plan
                let loadKgPrelude () =
                    loadKgPreludeForAgent false agent plan (fun sid dir -> knowledgeGraphRuntime.BuildPreludeForSession(sid, dir))
                let buildCaps encoded capsFiles prelude =
                    buildCapsMessages VibeFs.Shell.FileSys.sha256HexTruncated encoded directory capsFiles prelude
                let! final =
                    runHostMessagesTransform
                        reviewStore
                        sessionID
                        IfStoreEmpty
                        replayTexts
                        plan
                        backlogOps
                        MessagingCodec.encodeMessages
                        dedupFn
                        loadCaps
                        loadKgPrelude
                        buildCaps
                if not cleaned.IsEmpty then replaceArrayInPlace messagesArr final
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