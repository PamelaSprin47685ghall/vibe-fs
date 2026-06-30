module Wanxiangshu.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.NudgeState
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Kernel.ReviewReplayPolicy
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.SerialStateHolder
open Wanxiangshu.Opencode.MessagingCodec

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

let private collectSnapshot (client: obj) (sessionID: SessionId) : JS.Promise<SessionSnapshot option * obj array option> =
    promise {
        try
            let sessionIDStr = Id.sessionIdValue sessionID
            match getSessionApiFromClient client with
            | Error _ -> return None, None
            | Ok session ->
                let! todoResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "todo" session
                let openTodosFromApi = decodeTodos (Dyn.get todoResp "data")
                let! messagesResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "messages" session
                let messagesData = Dyn.get messagesResp "data"
                let openTodos =
                    if not (List.isEmpty openTodosFromApi) then openTodosFromApi
                    else recoverOpenTodosFromMessages messagesData
                let historyTexts =
                    if Dyn.isArray messagesData then
                        MessagingCodec.decodeMessages (messagesData :?> obj array)
                        |> Messaging.flatten
                        |> textsFromFlatParts
                        |> Seq.toList
                    else
                        []
                let lastAssistantMessage, agentFromMessage, alreadyNudged =
                    decodeLastAssistant messagesData
                let lastAssistantIsCompaction =
                    if Dyn.isArray messagesData then
                        (messagesData :?> obj array)
                        |> Array.tryFindBack (fun msg ->
                            let info = Dyn.get msg "info"
                            not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")
                        |> Option.map (fun msg -> Dyn.str (Dyn.get msg "info") "agent")
                        |> Option.exists (fun a -> a = "compaction")
                    else false
                let snapshot =
                    { todos = openTodos
                      lastAssistantMessage = lastAssistantMessage
                      isLoopActive = historyTexts |> reviewTaskFromTexts |> Option.isSome
                      alreadyNudged = alreadyNudged
                      agentFromMessage = agentFromMessage
                      lastAssistantIsCompaction = lastAssistantIsCompaction
                      anchorPromptIssued = false }
                return Some snapshot, Some (if Dyn.isArray messagesData then messagesData :?> obj array else [||])
        with _ ->
            return None, None
    }

let private sendNudge (client: obj) (sessionID: SessionId) (agentOpt: string option) (promptText: string) : JS.Promise<unit> =
    promise {
        let body = createPromptBody agentOpt promptText
        let promptArg = box {| path = box {| id = Id.sessionIdValue sessionID |}; body = body |}
        match getSessionApiFromClient client with
        | Error _ -> ()
        | Ok session -> do! invoke1 promptArg "prompt" session |> Promise.map ignore
    }

let private sendNudgeOutcome (client: obj) (sessionID: SessionId) (promptText: string) (agentOpt: string option) : JS.Promise<SendOutcome> =
    promise {
        let! caught = sendNudge client sessionID agentOpt promptText |> Promise.result
        return
            match caught with
            | Ok () -> Delivered
            | Error error ->
                match ErrorClassify.translateJsError error with
                | MessageAborted -> Aborted
                | SessionBusy -> Busy
                | _ -> Failed
    }

let private runNudgeFlow (holder: StateHolder<NudgeShellState>) (client: obj)
                          (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
                          (registry: ChildAgentRegistry)
                          (backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession)
                          (sessionID: SessionId) : JS.Promise<unit> =
    let sid = Id.sessionIdValue sessionID
    let takeSnapshot () =
        promise {
            let! snapshotOpt, messagesOpt = collectSnapshot client sessionID
            match snapshotOpt with
            | None -> return None
            | Some snapshot ->
                match snapshot.lastAssistantIsCompaction with
                | true ->
                    match messagesOpt with
                    | Some messagesArr ->
                        let messagesList = MessagingCodec.decodeMessages messagesArr
                        let cleaned = Messaging.stripSyntheticBySource messagesList
                        let backlogEntries = backlogSession.GetOrRebuildBacklog(sid, cleaned)
                        let promptText =
                            let extractAnchorTexts () =
                                cleaned
                                |> List.collect (fun m -> m.parts)
                                |> List.choose (function
                                    | TextPart t -> Some t
                                    | ToolPart(_, _, Some s, _) -> Some s.output
                                    | _ -> None)
                            Wanxiangshu.Kernel.BacklogProjectionCore.buildCompactionAnchorPrompt backlogEntries extractAnchorTexts
                        if promptText <> "" then do! sendNudge client sessionID snapshot.agentFromMessage promptText
                        holder.Mutate(fun state ->
                            { state with compactionAnchorsIssued = Set.add sid state.compactionAnchorsIssued }, ())
                    | None -> ()
                    return Some { snapshot with anchorPromptIssued = true }
                | _ -> return Some snapshot
        }
    let sendNudgeFn promptText agentOpt = sendNudgeOutcome client sessionID promptText agentOpt
    NudgeRuntime.runNudgeFlowCore holder registry.LookupChildAgent sid takeSnapshot sendNudgeFn

let startNudgeFlow (holder: StateHolder<NudgeShellState>) (client: obj)
                    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
                    (registry: ChildAgentRegistry)
                    (backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession)
                    (sessionID: SessionId) : unit =
    runNudgeFlow holder client reviewStore registry backlogSession sessionID |> Promise.start
