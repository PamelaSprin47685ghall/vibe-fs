module VibeFs.Opencode.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicProjection
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Kernel.MessageDedup
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.Config
open VibeFs.Kernel.Methodology
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.MagicTodo
open VibeFs.Opencode.MessagingCodec
open VibeFs.Opencode.CapsCodec
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.TreeSitterShell
open VibeFs.Shell.Dyn

let private defaultExcludedAgents = set [ "browser"; "investigator"; "executor"; "title"; "compaction"; "bookkeeper" ]

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private setOutput (o: obj) (v: string) : unit = o?output <- v

let private replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if System.Object.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore

let private extractSessionID (messages: Message<obj> list) : string =
    messages
    |> List.tryPick (fun m -> if m.info.sessionID <> "" then Some m.info.sessionID else None)
    |> Option.defaultValue ""

let private agentFromMessageInfo (registry: ChildAgentRegistry) (info: MessageInfo<obj>) : string option =
    if info.agent <> "" then Some info.agent
    elif info.sessionID <> "" then registry.LookupChildAgent(info.sessionID)
    else None

let private resolveAgentFromMessages (registry: ChildAgentRegistry) (messages: Message<obj> list) : string option =
    let fromInfo = agentFromMessageInfo registry
    let tryAgentBack (predicate: Message<obj> -> bool) : string option =
        messages
        |> List.filter predicate
        |> List.tryLast
        |> Option.bind (fun m -> fromInfo m.info)
    [
        tryAgentBack (fun m -> m.info.role = User && m.source = Native)
        tryAgentBack (fun m -> m.info.role = Assistant)
        tryAgentBack (fun m -> fromInfo m.info |> Option.isSome)
    ]
    |> List.tryPick id

let private resolveAgent (registry: ChildAgentRegistry) (input: obj) (messages: Message<obj> list) : string =
    let explicit = Dyn.str input "agent"
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent(Dyn.str input "sessionID") with
        | Some a -> a
        | None -> resolveAgentFromMessages registry messages |> Option.defaultValue "build"

let private applyReadDedup (messages: obj array) : unit =
    if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
    else
        let seenByPath = createObj []
        for i = 0 to messages.Length - 1 do
            let message = messages.[i]
            if not (Dyn.isNullish message) then
                let parts = Dyn.get message "parts"
                if not (Dyn.isNullish parts) && Dyn.isArray parts then
                    let partsArr = parts :?> obj array
                    for j = 0 to partsArr.Length - 1 do
                        let part = partsArr.[j]
                        if not (Dyn.isNullish part)
                           && Dyn.str part "type" = "tool"
                           && Dyn.str part "tool" = "read" then
                            let state = Dyn.get part "state"
                            if not (Dyn.isNullish state) then
                                let output = Dyn.get state "output"
                                if not (Dyn.isNullish output) && Dyn.typeIs output "string" then
                                    let currentOutput = string output
                                    let pathKey =
                                        match extractFilePaths (Dyn.get state "input") with
                                        | path :: _ -> path
                                        | [] -> ""
                                    let payload = { path = pathKey; content = currentOutput }
                                    let pathState =
                                        let existing = Dyn.get seenByPath pathKey
                                        if Dyn.isNullish existing then { seenContents = [] }
                                        else unbox<DedupState> existing
                                    let verdict, nextState = processDedup pathState payload
                                    setKey seenByPath pathKey (box nextState)
                                    match verdict with
                                    | AlreadySeen -> setOutput state (noChangeEnvelope ())
                                    | NewContent _ -> ()

module private CapsFileCache =
    let mutable private cache = Map.empty<string, VibeFs.Kernel.CapsFormat.CapsFile list>

    let getOrLoad (sessionID: string) (directory: string) : JS.Promise<VibeFs.Kernel.CapsFormat.CapsFile list> =
        match Map.tryFind sessionID cache with
        | Some files -> Promise.lift files
        | None ->
            promise {
                let! files = VibeFs.Shell.WorkspaceFiles.findCapsFiles directory
                if not (Map.containsKey sessionID cache) then cache <- Map.add sessionID files cache
                return files
            }

let private extractHistoryTexts (messages: Message<obj> list) =
    messages
    |> Messaging.flatten
    |> List.map (fun fp ->
        match fp.part with
        | TextPart text -> text
        | ToolPart(_, _, Some state, _) -> state.output
        | _ -> "")

/// After an opencode restart the in-memory review store is empty, but the
/// dialogue history still carries the activation / cancel / accept markers.
/// Replay them and re-activate the session iff the store has no record of it
/// yet — never clobber a live (possibly locked) review with a rebuild.  The
/// history is the single source of truth; the store is its re-buildable
/// projection.
let private reconstructReviewState (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (sessionID: string) (messages: Message<obj> list) : unit =
    if sessionID = "" then ()
    else
        match reviewStore.getReviewState sessionID with
        | Some _ -> ()
        | None ->
            match inferReviewTaskFromTexts (extractHistoryTexts messages) with
            | Some task ->
                reviewStore.activateReview(sessionID, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            | None -> ()

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (magicSession: MagicSession) (knowledgeGraphRuntime: KnowledgeGraphRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let messages = Dyn.get output "messages"
        if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
        else
            let messagesArr = messages :?> obj array
            if messagesArr.Length = 0 then ()
            else
                let messagesList = MessagingCodec.decodeMessages messagesArr
                let agent = resolveAgent registry input messagesList
                let sessionID = extractSessionID messagesList
                reconstructReviewState reviewStore sessionID messagesList
                let cleaned = Messaging.stripSyntheticBySource messagesList
                if cleaned.IsEmpty then ()
                else
                    let excluded = defaultExcludedAgents |> Set.contains agent
                    let backlog = magicSession.GetOrRebuildBacklog(sessionID, cleaned)
                    let afterMagic = if excluded then cleaned else projectMagicFor magicSession.Host cleaned backlog false sessionID
                    let encoded = MessagingCodec.encodeMessages afterMagic
                    if not excluded then applyReadDedup encoded
                    let! capsFiles =
                        if excluded then Promise.lift ([]: CapsFile list)
                        else CapsFileCache.getOrLoad sessionID directory
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

let compactingHandlerFor (host: Host) (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        ignore host
        ignore magicSession
        ignore input
        ignore output
        ()
    }

let compactingHandler (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    compactingHandlerFor opencode magicSession input output

let systemTransform (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let systemObj = output?system
        if not (Dyn.isNullish systemObj) then
            systemObj?length <- 0
    }
