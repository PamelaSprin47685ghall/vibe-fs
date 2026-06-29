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
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.ChatTransformOutputCodec
open Wanxiangshu.Shell.JsArrayMutate
open Wanxiangshu.Shell.SembleMcp
open Wanxiangshu.Shell.SembleSearch

let private extractSessionID (messages: Message<obj> list) : string =
    messages
    |> List.tryPick (fun m -> if m.info.sessionID <> "" then Some m.info.sessionID else None)
    |> Option.defaultValue ""

let private injectSembleResults
    (directory: string)
    (final: obj array)
    (agent: string)
    (sessionID: string)
    : JS.Promise<obj array> =
    promise {
        let messages = MessagingCodec.decodeMessages final
        match SembleSearch.breakpointStart sessionID with
        | None ->
            trace "DECIDE" $"reseed: no prior breakpoint, skip this turn (agent={agent}, len={final.Length})"
            SembleSearch.markBreakpoint sessionID final.Length
            return final
        | Some stored when stored > List.length messages ->
            trace "DECIDE" $"reseed: breakpoint {stored} > len {List.length messages}, compaction reset"
            SembleSearch.markBreakpoint sessionID final.Length
            return final
        | Some startIndex ->
            let context = SembleSearch.extractContextFromMessages startIndex messages
            if context.Length = 0 then
                trace "DECIDE" $"skip: empty context (start={startIndex}, len={final.Length})"
                return final
            else
                let! results = search context directory 3
                if results.IsEmpty then
                    trace "DECIDE" $"skip: no results (start={startIndex}, len={final.Length})"
                    return final
                else
                    let rec findLastAssistant i =
                        if i < 0 then None
                        else
                            let m = final.[i]
                            let role = Wanxiangshu.Shell.Dyn.str (Wanxiangshu.Shell.Dyn.get m "info") "role"
                            if role = "assistant" then Some m else findLastAssistant (i - 1)
                    match findLastAssistant (final.Length - 1) with
                    | None ->
                        trace "DECIDE" "skip: no assistant to attach reads"
                        return final
                    | Some lastAssistant ->
                        let assistantId = Wanxiangshu.Shell.Dyn.str (Wanxiangshu.Shell.Dyn.get lastAssistant "info") "id"
                        let! newToolParts = SembleSearch.buildReadToolParts assistantId sessionID results
                        if Array.isEmpty newToolParts then
                            trace "DECIDE" "skip: no tool parts"
                            return final
                        else
                            let originalParts = Wanxiangshu.Shell.Dyn.get lastAssistant "parts"
                            let cleaned =
                                if Wanxiangshu.Shell.Dyn.isNullish originalParts || not (Wanxiangshu.Shell.Dyn.isArray originalParts) then [||]
                                else (originalParts :?> obj array) |> Array.filter (fun p -> not ((Wanxiangshu.Shell.Dyn.str p "callID").StartsWith("semble-call-")))
                            let combined = Array.append cleaned newToolParts
                            lastAssistant?parts <- box combined
                            SembleSearch.markBreakpoint sessionID final.Length
                            SembleSearch.dumpInjection sessionID agent context results newToolParts.Length
                            return final
    }

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope) (backlogSession: BacklogSession) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) (client: obj) (input: obj) (output: obj) : JS.Promise<unit> =
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
                        buildCaps
                if not cleaned.IsEmpty then
                    let! injected =
                        if agent = "investigator" then
                            injectSembleResults directory final agent sessionID
                        else
                            Wanxiangshu.Shell.SembleMcp.trace "DECIDE" $"skip: agent={agent} (not investigator)"
                            Promise.lift final
                    replaceArrayInPlace messagesArr injected
    }

let buildCompactionAnchorPrompt (backlogEntries: BacklogEntry list) (messages: Message<obj> list) : string =
    let extractAnchorTexts () =
        messages
        |> List.collect (fun m -> m.parts)
        |> List.choose (function
            | TextPart t -> Some t
            | ToolPart(_, _, Some s, _) -> Some s.output
            | _ -> None)
    BacklogProjectionCore.buildCompactionAnchorPrompt backlogEntries extractAnchorTexts

let compactingHandlerFor (_host: Host) (backlogSession: BacklogSession) (_client: obj) (input: obj) (output: obj) : JS.Promise<unit> =
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

let compactingHandler (backlogSession: BacklogSession) (client: obj) (input: obj) (output: obj) : JS.Promise<unit> =
    compactingHandlerFor opencode backlogSession client input output

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }
