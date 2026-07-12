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
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Opencode.MessagingCodec
open Wanxiangshu.Opencode.CapsCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.ChatTransformOutputCodec
open Wanxiangshu.Shell.MessageTransformCommon

open Wanxiangshu.Shell.JsArrayMutate
open Wanxiangshu.Shell.SembleMcp
open Wanxiangshu.Shell.SembleSearch

let injectSembleIntoEncoded
    (directory: string)
    (agent: string)
    (sessionID: string)
    (encoded: obj array)
    : JS.Promise<obj array> =
    promise {
        if agent <> "investigator" && agent <> "reviewer" then
            SembleSearch.markBreakpoint sessionID encoded.Length
            return encoded
        else
            let messages = MessagingCodec.decodeMessages encoded

            match SembleSearch.breakpointStart sessionID with
            | None ->
                SembleSearch.markBreakpoint sessionID encoded.Length

                SembleMcp.trace
                    "DECIDE"
                    $"reseed: no prior breakpoint, skip this turn (agent={agent}, len={encoded.Length})"

                return encoded
            | Some stored when stored > List.length messages ->
                SembleSearch.markBreakpoint sessionID encoded.Length
                SembleMcp.trace "DECIDE" $"reseed: breakpoint {stored} > len {List.length messages}, compaction reset"
                return encoded
            | Some startIndex ->
                let context = SembleSearch.extractContextFromMessages startIndex messages

                if context.Length = 0 then
                    SembleMcp.trace "DECIDE" $"skip: empty context (start={startIndex}, len={encoded.Length})"
                    return encoded
                else
                    let! results = SembleSearch.search context directory 3

                    if results.IsEmpty then
                        SembleMcp.trace "DECIDE" $"skip: no results (start={startIndex}, len={encoded.Length})"
                        return encoded
                    else
                        let rec findLastAssistant i =
                            if i < 0 then
                                None
                            else
                                let m = encoded.[i]
                                let role = Wanxiangshu.Shell.Dyn.str (Wanxiangshu.Shell.Dyn.get m "info") "role"

                                if role = "assistant" then
                                    Some m
                                else
                                    findLastAssistant (i - 1)

                        match findLastAssistant (encoded.Length - 1) with
                        | None ->
                            SembleMcp.trace "DECIDE" "skip: no assistant to attach reads"
                            return encoded
                        | Some lastAssistant ->
                            let assistantId =
                                Wanxiangshu.Shell.Dyn.str (Wanxiangshu.Shell.Dyn.get lastAssistant "info") "id"

                            let newToolParts = SembleSearch.buildReadToolParts assistantId sessionID results

                            if Array.isEmpty newToolParts then
                                SembleMcp.trace "DECIDE" "skip: no tool parts"
                                return encoded
                            else
                                let originalParts = Wanxiangshu.Shell.Dyn.get lastAssistant "parts"

                                let cleaned =
                                    if
                                        Wanxiangshu.Shell.Dyn.isNullish originalParts
                                        || not (Wanxiangshu.Shell.Dyn.isArray originalParts)
                                    then
                                        [||]
                                    else
                                        (originalParts :?> obj array)
                                        |> Array.filter (fun p ->
                                            not ((Wanxiangshu.Shell.Dyn.str p "callID").StartsWith("semble-call-")))

                                let combined = Array.append cleaned newToolParts
                                lastAssistant?parts <- box combined
                                SembleSearch.markBreakpoint sessionID encoded.Length
                                SembleSearch.dumpInjection sessionID agent context results newToolParts.Length
                                return encoded
    }

let messagesTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
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
            let sessionID = extractSessionID messagesList
            let cleaned = Messaging.stripSyntheticBySource messagesList
            let isSub = registry.ResolveSubsessionParentID(Some sessionID) |> Option.isSome

            let projectionPolicy =
                if shouldExcludeAgentFromProjection agent false then
                    ProjectionPolicy.ExcludeProjection
                else
                    ProjectionPolicy.IncludeProjection

            let sembleInjectEnabled =
                (match projectionPolicy with
                 | ProjectionPolicy.ExcludeProjection -> false
                 | ProjectionPolicy.IncludeProjection -> true)
                && (agent = "investigator" || agent = "reviewer")

            let backlogOps =
                backlogSessionOpsFrom backlogSession.Host (fun sid msgs ->
                    backlogSession.GetOrRebuildBacklog(sid, msgs))

            let! maxInputTokens =
                Wanxiangshu.Shell.ContextBudgetUsageCodec.resolveMaxInputTokens [ client ] sessionID directory

            let getContextUsage encoded =
                Wanxiangshu.Shell.OpencodeContextBudgetObservation.tryCurrentUsage client sessionID encoded

            let plan =
                { SessionID = sessionID
                  Agent = agent
                  Directory = directory
                  ProjectionPolicy = projectionPolicy
                  IsSubagentSession = isSub
                  Cleaned = cleaned
                  RawArray = Some messagesArr
                  SembleInjectEnabled = sembleInjectEnabled
                  Scope = runtimeScope
                  MaxInputTokens = maxInputTokens
                  GetContextUsage = getContextUsage }

            let replayTexts () : JS.Promise<string seq> =
                Promise.lift (extractTextsFromEncodedMessages messagesArr)

            let injectFn policy encoded =
                match policy with
                | ProjectionPolicy.ExcludeProjection -> Promise.lift encoded
                | ProjectionPolicy.IncludeProjection -> injectSembleIntoEncoded directory agent sessionID encoded

            let loadCaps () =
                let parentSessionID =
                    registry.ResolveSubsessionParentID(Some sessionID)
                    |> Option.defaultValue sessionID

                let planWithParent =
                    { plan with
                        SessionID = parentSessionID }

                loadCapsForScope runtimeScope AllowEmptyDirectory planWithParent

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
                    injectFn
                    loadCaps
                    buildCaps

            replaceArrayInPlace messagesArr final
    }

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let private invokeClient (client: obj) (method_: string) (arg: obj) : JS.Promise<obj> =
    if Dyn.isNullish client then
        Promise.lift (unbox null)
    else
        match getSessionApiFromClient client with
        | Error _ -> Promise.lift (unbox null)
        | Ok session ->
            let api: obj = Dyn.get session method_

            if Dyn.isNullish api then
                Promise.lift (unbox null)
            else
                unbox<JS.Promise<obj>> (Dyn.callMethod1 session method_ arg)

let compactionAutocontinue (input: obj) (output: obj) : JS.Promise<unit> = promise { output?enabled <- true }

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }

let private zwsChar = "​"

let compactingTransform
    (registry: ChildAgentRegistry)
    (directory: string)
    (runtimeScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (client: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()

        let sessionID = Dyn.str input "sessionID"

        if sessionID <> "" then
            let fallbackRuntime =
                match runtimeScope.TryFindKey("fallbackRuntime") with
                | Some obj -> Some(unbox<Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState> obj)
                | None -> None

            let compactionId = "compact-" + System.Guid.NewGuid().ToString("N")
            do! Wanxiangshu.Shell.EventLogRuntime.appendCompactionStartedOrFail directory sessionID compactionId

            match fallbackRuntime with
            | Some fr ->
                fr.SetSessionOwner sessionID "Compaction"
                fr.SetActiveCompactionId(sessionID, compactionId)
                fr.SetCompacted sessionID false
                fr.SetCompactionContinuationObserved sessionID false
                let currentGen = fr.GetSessionGeneration sessionID
                fr.SetCompactionGeneration sessionID currentGen
            | None -> ()

            try
                let arg = box {| path = box {| id = sessionID |} |}
                let! resp = invokeClient client "messages" arg
                let data = Dyn.get resp "data"

                let messagesArr =
                    if not (Dyn.isNullish data) && Dyn.isArray data then
                        data :?> obj array
                    else
                        [||]

                if messagesArr.Length > 0 then
                    let messagesList = MessagingCodec.decodeMessages messagesArr
                    let cleaned = Messaging.stripSyntheticBySource messagesList
                    let backlog = backlogSession.GetOrRebuildBacklog(sessionID, cleaned)
                    let guidGen () = string (runtimeScope.RandomGen())

                    let result =
                        Wanxiangshu.Kernel.BacklogProjectionCore.compactingTransform cleaned backlog guidGen

                    let wrappedText =
                        match result with
                        | m :: _ ->
                            match m.parts with
                            | TextPart t :: _ -> t
                            | _ -> ""
                        | [] -> ""

                    let currentContext =
                        let c = Dyn.get output "context"

                        if not (Dyn.isNullish c) && Dyn.isArray c then
                            c :?> string array
                        else
                            [||]

                    output?context <- Array.append currentContext [| wrappedText |]
            with ex ->
                do!
                    Wanxiangshu.Shell.EventLogRuntime.appendCompactionSettledOrFail
                        directory
                        sessionID
                        compactionId
                        "failed"

                match fallbackRuntime with
                | Some fr ->
                    fr.SetSessionOwner sessionID "None"
                    fr.SetCompacted sessionID false
                | None -> ()

                return! Promise.reject ex
    }
