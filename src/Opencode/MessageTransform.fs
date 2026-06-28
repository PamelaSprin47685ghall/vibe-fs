module Wanxiangshu.Opencode.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ReviewReplayPolicy
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.MessageTransformHostHooks
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.ReadDedupOpenCode
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Opencode.MessagingCodec
open Wanxiangshu.Opencode.CapsCodec
open Wanxiangshu.Opencode.KnowledgeGraphRuntime
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.ChatTransformOutputCodec
open Wanxiangshu.Shell.JsArrayMutate

let private extractSessionID (messages: Message<obj> list) : string =
    messages
    |> List.tryPick (fun m -> if m.info.sessionID <> "" then Some m.info.sessionID else None)
    |> Option.defaultValue ""

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope) (backlogSession: BacklogSession) (knowledgeGraphRuntime: KnowledgeGraphRuntime) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) (client: obj) (input: obj) (output: obj) : JS.Promise<unit> =
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
                    buildCapsMessages Wanxiangshu.Shell.FileSys.sha256HexTruncated encoded directory capsFiles prelude
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

let compactingHandlerFor (_host: Host) (backlogSession: BacklogSession) (client: obj) (input: obj) (output: obj) : JS.Promise<unit> =
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
                    // Inline backlogEntries for front-matter block (projectionRootValue is private in BacklogProjectionCore)
                    let backlogEntries =
                        let entries =
                            backlogSession.GetOrRebuildBacklog(sessionID, cleaned)
                            |> List.map (fun be -> box (createObj [ "user_message", box [||]; "completed_work", box (be.report.Trim()) ]))
                        box (entries |> List.toArray)
                    let backlogBlock = [ frontMatterRoot backlogEntries ]
                    let anchorTexts =
                        cleaned
                        |> List.collect (fun m -> m.parts)
                        |> List.choose (function
                            | TextPart t -> Some t
                            | ToolPart(_, _, Some s, _) -> Some s.output
                            | _ -> None)
                    let anchorBlocks = anchorTexts |> List.collect PromptFrontMatter.extractFrontMatterFenceStrings
                    let allBlocks = backlogBlock @ anchorBlocks
                    if not allBlocks.IsEmpty && not (Dyn.isNullish client) && sessionID <> "" then
                        let promptText = PromptFrontMatter.renderCompactionAnchorPrompt allBlocks
                        try do! client?session?prompt(sessionID, box promptText) |> Promise.map ignore with _ -> ()
                    replaceArrayInPlace messagesArr encoded
    }

let compactingHandler (backlogSession: BacklogSession) (client: obj) (input: obj) (output: obj) : JS.Promise<unit> =
    compactingHandlerFor opencode backlogSession client input output

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }