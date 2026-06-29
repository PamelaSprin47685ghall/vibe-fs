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
                let injectFn _ encoded = Promise.lift encoded
                let loadCaps () =
                    loadCapsForScope runtimeScope RequireDirectory plan
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
                let backlogEntries = backlogSession.GetOrRebuildBacklog(sessionID, cleaned)
                let anchorTexts =
                    cleaned
                    |> List.collect (fun m -> m.parts)
                    |> List.choose (function
                        | TextPart t -> Some t
                        | ToolPart(_, _, Some s, _) -> Some s.output
                        | _ -> None)
                let promptText = BacklogProjectionCore.buildCompactionAnchorPrompt backlogEntries (fun () -> List.ofSeq anchorTexts)
                if not (System.String.IsNullOrEmpty(promptText)) && sessionID <> "" then
                    let session = Dyn.get deps "session"
                    if not (Dyn.isNullish session) then
                        let promptFn = Dyn.get session "prompt"
                        if not (Dyn.isNullish promptFn) then
                            let! chatHistory : obj[] =
                                promise {
                                    let getHist = Dyn.get deps "getChatHistory"
                                    if Dyn.isNullish getHist then return [||] else return! unbox<JS.Promise<obj[]>> (Dyn.call1 getHist sessionID)
                                }
                            let historyTexts = extractTextsFromEncodedMessages chatHistory
                            if Seq.exists hasCompactionAnchorPrompt historyTexts |> not then
                                try do! unbox<JS.Promise<unit>> (Dyn.call2 promptFn sessionID (box promptText)) with _ -> ()
                replaceArrayInPlace messagesArr encoded
    }