module VibeFs.Opencode.HookTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Kernel.Message
open VibeFs.Kernel.LoopMessages
open VibeFs.Opencode.HookSchema
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicProjection
open VibeFs.Kernel.MagicTodo
open VibeFs.Opencode.MagicTodo
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.Dedup
open VibeFs.Opencode.WikiRuntime

let private defaultExcludedAgents = [ "browser"; "investigator"; "executor"; "title" ]

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private setOutput (o: obj) (v: string) : unit = o?output <- v
let private resolvedUnit : JS.Promise<unit> = Promise.lift ()

let private replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if System.Object.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private resolveAgent (registry: ChildAgentRegistry) (input: obj) : string =
    let explicit = Dyn.str input "agent"
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent(Dyn.str input "sessionID") with
        | Some a -> a
        | None -> "manager"

let private extractSessionID (messages: obj array) : string =
    if messages.Length = 0 then ""
    else
        let info = messageInfo messages.[0]
        if Dyn.isNullish info then "" else infoSessionID info

let private resolveChatTools (host: Host) (agent: string) (existingTools: obj) : obj =
    let next = createObj []
    if not (Dyn.isNullish existingTools) then
        for key in objectKeys existingTools do
            if canUseForHost host agent key then
                setKey next key (Dyn.get existingTools key)
            else
                setKey next key (box false)
    next

let chatMessageFor (host: Host) (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let agent = resolveAgent registry input
        let sessionID = Id.sessionIdQuick (Dyn.str input "sessionID")
        do! nudgeHook.handleChatMessage(sessionID, agent, Dyn.get output "parts")
        let message = Dyn.get output "message"
        if not (Dyn.isNullish message) then
            let tools = Dyn.get message "tools"
            if not (Dyn.isNullish tools) then
                setKey message "tools" (resolveChatTools host agent tools)
    }

let chatMessage (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    chatMessageFor opencode registry nudgeHook input output

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
                                    | AlreadySeen -> setOutput state dedupMarker
                                    | NewContent _ -> ()

module private CapsFileCache =
    let private cache = System.Collections.Generic.Dictionary<string, VibeFs.Kernel.CapsFormat.CapsFile list>()

    let getOrLoad (sessionID: string) (directory: string) : JS.Promise<VibeFs.Kernel.CapsFormat.CapsFile list> =
        match cache.TryGetValue sessionID with
        | true, files -> Promise.lift files
        | false, _ ->
            promise {
                let! files = VibeFs.Shell.WorkspaceFiles.findCapsFiles directory
                if not (cache.ContainsKey sessionID) then cache.[sessionID] <- files
                return files
            }

let private extractHistoryTexts (messages: obj array) =
    messages
    |> Message.flatten
    |> List.map (fun fp ->
        let part = fp.part
        if Message.partIsText part then Message.partTextStr part
        elif Message.partIsTool part then Message.partToolOutput part
        else "")

/// After an opencode restart the in-memory review store is empty, but the
/// dialogue history still carries the activation / cancel / accept markers.
/// Replay them and re-activate the session iff the store has no record of it
/// yet — never clobber a live (possibly locked) review with a rebuild.  The
/// history is the single source of truth; the store is its re-buildable
/// projection.
let private reconstructReviewState (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (sessionID: string) (messages: obj array) : unit =
    if sessionID = "" then ()
    else
        match reviewStore.getReviewState sessionID with
        | Some _ -> ()
        | None ->
            match inferReviewTaskFromTexts (extractHistoryTexts messages) with
            | Some task ->
                reviewStore.activateReview(sessionID, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            | None -> ()

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (magicSession: MagicSession) (wikiRuntime: WikiRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let messages = Dyn.get output "messages"
        if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
        else
            let messagesArr = messages :?> obj array
            if messagesArr.Length = 0 then ()
            else
                let agent = resolveAgent registry input
                let sessionID = extractSessionID messagesArr
                reconstructReviewState reviewStore sessionID messagesArr
                let cleaned = stripSyntheticMessages messagesArr
                if cleaned.Length = 0 then ()
                else
                    if defaultExcludedAgents |> List.contains agent then
                        replaceArrayInPlace messagesArr cleaned
                    else
                        let backlog = magicSession.GetOrRebuildBacklog(sessionID, cleaned)
                        let afterMagic = projectMagicFor magicSession.Host cleaned backlog false sessionID
                        applyReadDedup afterMagic
                        if agent = "manager" then
                            do! wikiRuntime.StartMaintenanceIfDue(directory)
                        let! capsFiles = CapsFileCache.getOrLoad sessionID directory
                        let! wikiPrelude =
                            if agent = "manager" then wikiRuntime.BuildPreludeForSession(sessionID, directory)
                            else Promise.lift None
                        let final =
                            buildCapsMessages
                                VibeFs.Shell.FileSys.sha256HexTruncated
                                afterMagic
                                directory
                                defaultExcludedAgents
                                capsFiles
                                wikiPrelude
                        replaceArrayInPlace messagesArr final
    }

let compactingHandlerFor (host: Host) (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let sessionID = Dyn.str input "sessionID"
        let backlog = magicSession.GetOrRebuildBacklog(sessionID, [||])
        if backlog.IsEmpty then ()
        else
            let context = Dyn.get output "context"
            if not (Dyn.isNullish context) && Dyn.isArray context then
                let hint = "Preserve the latest " + magicTodoToolNameFor host + " result and the complete Magic Todo backlog in the summary. If earlier user messages are folded, rewrite them into that todo summary as work-period user updates instead of preserving raw user messages verbatim."
                (box context)?push(box hint) |> ignore
    }

let compactingHandler (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    compactingHandlerFor opencode magicSession input output

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let toolID = Dyn.str input "toolID"
        if toolID = "coder" || toolID = "investigator" then
            rewriteToolJsonSchema setKey stripUiFromJsonSchema output
        elif toolID = magicTodoToolNameFor host then
            match host with
            | Opencode ->
                setKey output "description" (box toolDescription)
                setKey output "jsonSchema" (buildMagicTodoSchema ())
            | Mimocode ->
                setKey output "description" (box fusedTaskToolDescription)
                rewriteToolJsonSchema setKey mergeMagicReportIntoTaskSchema output
    }

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    toolDefinitionFor opencode input output

let eventHandler (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) : JS.Promise<unit> =
    promise {
        let event = Dyn.get input "event"
        if Dyn.str event "type" = "stream-abort" then
            let props = Dyn.get event "properties"
            let sessionID =
                if Dyn.isNullish props then "loop"
                else
                    let s = Dyn.str props "sessionID"
                    if s = "" then "loop" else s
            reviewStore.deactivateReview sessionID
    }

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = resolvedUnit
let noopEvent (_a: obj) : JS.Promise<unit> = resolvedUnit
