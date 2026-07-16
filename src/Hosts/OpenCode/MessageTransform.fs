module Wanxiangshu.Hosts.Opencode.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.MessageTransform.HostHooks
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Hosts.Opencode.AgentConfig
open Wanxiangshu.Hosts.Opencode.BacklogSession
open Wanxiangshu.Hosts.Opencode.MessagingCodec
open Wanxiangshu.Hosts.Opencode.CapsCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ChatTransformOutputCodec
open Wanxiangshu.Runtime.HostMessagePartCodec
open Wanxiangshu.Kernel.FallbackKernel.Types

open Wanxiangshu.Runtime.JsArrayMutate
open Wanxiangshu.Runtime.SembleMcp
open Wanxiangshu.Runtime.SembleSearch

let private getBreakpointState (agent: string) (sessionID: string) (messages: Message<obj> list) (encodedLen: int) =
    if agent <> "investigator" && agent <> "reviewer" then
        SembleSearch.markBreakpoint sessionID encodedLen
        Error encodedLen
    else
        match SembleSearch.breakpointStart sessionID with
        | None ->
            SembleSearch.markBreakpoint sessionID encodedLen
            SembleMcp.trace "DECIDE" $"reseed: no prior breakpoint, skip this turn (agent={agent}, len={encodedLen})"
            Error encodedLen
        | Some stored when stored > List.length messages ->
            SembleSearch.markBreakpoint sessionID encodedLen
            SembleMcp.trace "DECIDE" $"reseed: breakpoint {stored} > len {List.length messages}, compaction reset"
            Error encodedLen
        | Some startIndex ->
            let context = SembleSearch.extractContextFromMessages startIndex messages

            if context.Length = 0 then
                SembleMcp.trace "DECIDE" $"skip: empty context (start={startIndex}, len={encodedLen})"
                Error encodedLen
            else
                Ok context

let private findLastAssistant (encoded: obj array) =
    let rec loop i =
        if i < 0 then
            None
        else
            let m = encoded.[i]
            let role = Wanxiangshu.Runtime.Dyn.str (Wanxiangshu.Runtime.Dyn.get m "info") "role"
            if role = "assistant" then Some m else loop (i - 1)

    loop (encoded.Length - 1)

let private performSembleSearchAndAttach
    (directory: string)
    (agent: string)
    (sessionID: string)
    (encoded: obj array)
    (context: string)
    : JS.Promise<obj array> =
    promise {
        let! results = SembleSearch.search context directory 3

        if results.IsEmpty then
            SembleMcp.trace "DECIDE" $"skip: no results (context len={context.Length}, len={encoded.Length})"
            return encoded
        else
            match findLastAssistant encoded with
            | None ->
                SembleMcp.trace "DECIDE" "skip: no assistant to attach reads"
                return encoded
            | Some lastAssistant ->
                let assistantId =
                    Wanxiangshu.Runtime.Dyn.str (Wanxiangshu.Runtime.Dyn.get lastAssistant "info") "id"

                let newToolParts = SembleSearch.buildReadToolParts assistantId sessionID results

                if Array.isEmpty newToolParts then
                    SembleMcp.trace "DECIDE" "skip: no tool parts"
                    return encoded
                else
                    let originalParts = Wanxiangshu.Runtime.Dyn.get lastAssistant "parts"

                    let cleaned =
                        if
                            Wanxiangshu.Runtime.Dyn.isNullish originalParts
                            || not (Wanxiangshu.Runtime.Dyn.isArray originalParts)
                        then
                            [||]
                        else
                            (originalParts :?> obj array)
                            |> Array.filter (fun p ->
                                not ((Wanxiangshu.Runtime.Dyn.str p "callID").StartsWith("semble-call-")))

                    lastAssistant?parts <- box (Array.append cleaned newToolParts)
                    SembleSearch.markBreakpoint sessionID encoded.Length
                    SembleSearch.dumpInjection sessionID agent context results newToolParts.Length
                    return encoded
    }

let injectSembleIntoEncoded
    (directory: string)
    (agent: string)
    (sessionID: string)
    (encoded: obj array)
    : JS.Promise<obj array> =
    promise {
        let messages = MessagingCodec.decodeMessages encoded

        match getBreakpointState agent sessionID messages encoded.Length with
        | Error _ -> return encoded
        | Ok context -> return! performSembleSearchAndAttach directory agent sessionID encoded context
    }

let cleanupCapsEpochBySession (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope) (sessionID: string) : unit =
    if sessionID <> "" then
        let sessionKey = "caps_epoch_session_" + sessionID

        match runtimeScope.TryFindKey(sessionKey) with
        | Some epObj ->
            let epoch = epObj :?> string
            runtimeScope.Remove(sessionKey)
            runtimeScope.Remove("caps_epoch_reverse_session_" + epoch)
            let reverseConvKey = "caps_epoch_reverse_conv_" + epoch

            match runtimeScope.TryFindKey(reverseConvKey) with
            | Some ckObj ->
                let ck = ckObj :?> string
                runtimeScope.Remove("caps_epoch_conv_" + ck)
                runtimeScope.Remove(reverseConvKey)
            | None -> ()
        | None -> ()

let private findExistingEpoch (msgs: Message<obj> list) =
    let rec loop =
        function
        | [] -> None
        | msg :: rest ->
            let id = msg.info.id

            if id.StartsWith "caps-synth-user-" then
                Some(id.Substring("caps-synth-user-".Length))
            elif id.StartsWith "caps-synth-assistant-" then
                Some(id.Substring("caps-synth-assistant-".Length))
            elif id.StartsWith "caps-synth-ack-" then
                Some(id.Substring("caps-synth-ack-".Length))
            else
                loop rest

    loop msgs

let private findFirstNativeUserMsgId (msgs: Message<obj> list) =
    let rec loop =
        function
        | [] -> None
        | msg :: rest ->
            let id = msg.info.id

            let isSynth =
                id.StartsWith "caps-synth-"
                || id.StartsWith "backlog-projection-"
                || id.StartsWith "backlog-prefix-"
                || id.StartsWith "semble-call-"
                || id.StartsWith "nudge-"
                || id.StartsWith "context-budget-nudge-"

            if msg.info.role = User && not isSynth && id <> "" then
                Some id
            else
                loop rest

    loop msgs

let private resolveCapsEpoch
    (messagesList: Message<obj> list)
    (sessionID: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : string =
    match findExistingEpoch messagesList with
    | Some ep when ep <> "" -> ep
    | _ ->
        let convKeyOpt = findFirstNativeUserMsgId messagesList

        let tryFindEpoch () =
            match convKeyOpt with
            | Some ck ->
                match runtimeScope.TryFindKey("caps_epoch_conv_" + ck) with
                | Some ep -> Some(ep :?> string)
                | _ ->
                    if sessionID <> "" then
                        runtimeScope.TryFindKey("caps_epoch_session_" + sessionID)
                        |> Option.map (fun x -> x :?> string)
                    else
                        None
            | _ ->
                if sessionID <> "" then
                    runtimeScope.TryFindKey("caps_epoch_session_" + sessionID)
                    |> Option.map (fun x -> x :?> string)
                else
                    None

        match tryFindEpoch () with
        | Some ep ->
            if sessionID <> "" then
                runtimeScope.Add("caps_epoch_session_" + sessionID, box ep)
                runtimeScope.Add("caps_epoch_reverse_session_" + ep, box sessionID)

            ep
        | None ->
            match convKeyOpt, sessionID with
            | None, "" -> ""
            | _ ->
                let newEpoch = "epoch-" + System.Guid.NewGuid().ToString("N")

                match convKeyOpt with
                | Some ck ->
                    runtimeScope.Add("caps_epoch_conv_" + ck, box newEpoch)
                    runtimeScope.Add("caps_epoch_reverse_conv_" + newEpoch, box ck)
                | None -> ()

                if sessionID <> "" then
                    runtimeScope.Add("caps_epoch_session_" + sessionID, box newEpoch)
                    runtimeScope.Add("caps_epoch_reverse_session_" + newEpoch, box sessionID)

                newEpoch

let private maxInputTokensCache =
    System.Collections.Generic.Dictionary<string, int>()

let private resolveSessionID (input: obj) (messagesList: Message<obj> list) : string =
    let sid1 = Dyn.str input "sessionID"

    if sid1 <> "" then
        sid1
    else
        let sid2 = Dyn.str input "sessionId"

        if sid2 <> "" then
            sid2
        else
            let sid3 = Dyn.str input "session_id"
            if sid3 <> "" then sid3 else extractSessionID messagesList

let private resolveMaxInputTokens (sessionID: string) (client: obj) (directory: string) : JS.Promise<int> =
    match maxInputTokensCache.TryGetValue(sessionID) with
    | true, limit -> Promise.lift limit
    | _ ->
        promise {
            let! limit =
                Wanxiangshu.Runtime.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory

            maxInputTokensCache.[sessionID] <- limit
            return limit
        }

let private runHostMessagesTransformExecution
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (sessionID: string)
    (agent: string)
    (isSub: bool)
    (messagesList: Message<obj> list)
    (messagesArr: obj array)
    (sembleInjectEnabled: bool)
    (maxInputTokens: int)
    (client: obj)
    (capsEpoch: string)
    (plan: MessageTransformPlan)
    : JS.Promise<unit> =
    promise {
        let backlogOps =
            backlogSessionOpsFrom backlogSession.Host (fun sid msgs -> backlogSession.GetOrRebuildBacklog(sid, msgs))

        let injectFn policy encoded =
            match policy with
            | BacklogProjectionPolicy.Exclude -> Promise.lift encoded
            | BacklogProjectionPolicy.Include -> injectSembleIntoEncoded directory agent sessionID encoded

        let loadCaps () =
            promise {
                let parentSessionID =
                    registry.ResolveSubsessionParentID(Some sessionID)
                    |> Option.defaultValue sessionID

                let! caps =
                    loadCapsForScope
                        runtimeScope
                        AllowEmptyDirectory
                        { plan with
                            SessionID = parentSessionID }

                return caps |> List.sortBy (fun cf -> cf.label, cf.filePath)
            }

        let buildCaps encoded capsFiles prelude =
            buildCapsMessages
                Wanxiangshu.Runtime.FileSys.sha256HexTruncated
                capsEpoch
                encoded
                directory
                capsFiles
                prelude

        let! final =
            runHostMessagesTransform
                reviewStore
                sessionID
                plan
                backlogOps
                MessagingCodec.encodeMessages
                injectFn
                loadCaps
                buildCaps

        replaceArrayInPlace messagesArr final
    }

let messagesTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        match tryGetMessagesArrayFromOutput output with
        | None -> ()
        | Some messagesArr ->
            let messagesList = MessagingCodec.decodeMessages messagesArr
            let agent = resolveMessagesTransformAgent registry input messagesList "build"
            let sessionID = resolveSessionID input messagesList
            let capsEpoch = resolveCapsEpoch messagesList sessionID runtimeScope
            let isSub = registry.ResolveSubsessionParentID(Some sessionID) |> Option.isSome

            let p =
                Wanxiangshu.Kernel.MessageTransformPolicy.getBacklogProjectionPolicy agent isSub

            let caps =
                Wanxiangshu.Kernel.MessageTransformPolicy.getCapsInjectionPolicy agent isSub

            let par =
                Wanxiangshu.Kernel.MessageTransformPolicy.getParallelHintPolicy agent isSub

            let budget =
                Wanxiangshu.Kernel.MessageTransformPolicy.getContextBudgetPolicy agent isSub

            let! maxInputTokens = resolveMaxInputTokens sessionID client directory

            let plan =
                { SessionID = sessionID
                  Agent = agent
                  Directory = directory
                  ProjectionPolicy =
                    (if p = BacklogProjectionPolicy.Include then
                         ProjectionPolicy.IncludeProjection
                     else
                         ProjectionPolicy.ExcludeProjection)
                  BacklogProjectionPolicy = p
                  CapsInjectionPolicy = caps
                  ParallelHintPolicy = par
                  ContextBudgetPolicy = budget
                  IsSubagentSession = isSub
                  Cleaned = messagesList
                  RawArray = Some messagesArr
                  SembleInjectEnabled =
                    (p = BacklogProjectionPolicy.Include
                     && (agent = "investigator" || agent = "reviewer"))
                  Scope = runtimeScope
                  MaxInputTokens = maxInputTokens
                  GetContextUsage =
                    (fun encoded ->
                        Wanxiangshu.Runtime.OpencodeContextBudgetObservation.tryCurrentUsage client sessionID encoded) }

            do!
                runHostMessagesTransformExecution
                    registry
                    directory
                    runtimeScope
                    backlogSession
                    reviewStore
                    sessionID
                    agent
                    isSub
                    messagesList
                    messagesArr
                    (p = BacklogProjectionPolicy.Include
                     && (agent = "investigator" || agent = "reviewer"))
                    maxInputTokens
                    client
                    capsEpoch
                    plan
    }
