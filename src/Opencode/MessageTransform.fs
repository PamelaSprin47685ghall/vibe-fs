module VibeFs.Opencode.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicProjection
open VibeFs.Kernel.Dedup
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.Config
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.MagicTodo
open VibeFs.Opencode.MessagingCodec
open VibeFs.Opencode.CapsCodec
open VibeFs.Opencode.EditPlusState
open VibeFs.Opencode.WikiRuntime
open VibeFs.Shell.ChildAgentRegistry

let private defaultExcludedAgents = set [ "browser"; "investigator"; "executor"; "title"; "bookkeeper" ]

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private setOutput (o: obj) (v: string) : unit = o?output <- v

let private replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if System.Object.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore

let private resolveAgent (registry: ChildAgentRegistry) (input: obj) : string =
    let explicit = Dyn.str input "agent"
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent(Dyn.str input "sessionID") with
        | Some a -> a
        | None -> "manager"

let private extractSessionID (messages: Message list) : string =
    messages
    |> List.tryPick (fun m -> if m.info.sessionID <> "" then Some m.info.sessionID else None)
    |> Option.defaultValue ""

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

let private extractHistoryTexts (messages: Message list) =
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
let private reconstructReviewState (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (sessionID: string) (messages: Message list) : unit =
    if sessionID = "" then ()
    else
        match reviewStore.getReviewState sessionID with
        | Some _ -> ()
        | None ->
            match inferReviewTaskFromTexts (extractHistoryTexts messages) with
            | Some task ->
                reviewStore.activateReview(sessionID, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            | None -> ()

let private countLinesInOutput (output: string) : int =
    if output = "" then 0
    else
        output.Split('\n')
        |> Array.filter (fun line ->
            if not (line.Contains("|")) then false
            else
                let tag = line.Split('|').[0]
                tag.Length > 0 && tag |> Seq.forall (fun c -> (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')))
        |> Array.length

let private resolveEndForReplay (state: EditPlusState) (input: obj) : int option =
    let endInc = Dyn.get input "endInclusive"
    if not (Dyn.isNullish endInc) then
        let endNum = if Dyn.typeIs endInc "string" then tagToNum (string endInc) else unbox<int> endInc
        match state.Registry.Resolve(endNum) with
        | TagValid(_, line) -> Some(line + 1)
        | _ -> None
    else
        let endExc = Dyn.get input "endExclusive"
        if not (Dyn.isNullish endExc) then
            let endNum = if Dyn.typeIs endExc "string" then tagToNum (string endExc) else unbox<int> endExc
            match state.Registry.Resolve(endNum) with
            | TagValid(_, line) -> Some line
            | _ -> None
        else None

let private replayEditPlusState (editPlusState: EditPlusState option) (messages: Message list) : unit =
    match editPlusState with
    | None -> ()
    | Some state ->
        state.Reset()
        for fp in Messaging.flatten messages do
            match fp.part with
            | ToolPart(toolName, _, Some st, _) when st.status = "completed" ->
                if toolName = "read" then
                    let input = st.input
                    if not (Dyn.isNullish input) then
                        let path = Dyn.str input "path"
                        if path <> "" && not (state.Registry.HasFile(path)) then
                            let lineCount = countLinesInOutput st.output
                            state.Registry.Assign(path, 0, lineCount + 1) |> ignore
                elif toolName = "edit" then
                    let input = st.input
                    if not (Dyn.isNullish input) then
                        let beginTag = Dyn.get input "begin"
                        let content = Dyn.str input "content"
                        if not (Dyn.isNullish beginTag) then
                            let beginNum = if Dyn.typeIs beginTag "string" then tagToNum (string beginTag) else unbox<int> beginTag
                            match state.Registry.Resolve(beginNum) with
                            | TagValid(path, bLine) ->
                                match resolveEndForReplay state input with
                                | Some eLine ->
                                    let ins = if content = "" then [||] else splitLines content
                                    state.Registry.Edit(path, bLine, eLine, ins.Length) |> ignore
                                | None -> ()
                            | _ -> ()
            | _ -> ()

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (magicSession: MagicSession) (wikiRuntime: WikiRuntime) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (editPlusState: VibeFs.Opencode.EditPlusState.EditPlusState option) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let messages = Dyn.get output "messages"
        if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
        else
            let messagesArr = messages :?> obj array
            if messagesArr.Length = 0 then ()
            else
                let agent = resolveAgent registry input
                let messagesList = MessagingCodec.decodeMessages messagesArr
                let sessionID = extractSessionID messagesList
                reconstructReviewState reviewStore sessionID messagesList
                replayEditPlusState editPlusState messagesList
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
                    let! wikiPrelude =
                        if not excluded && canUse agent "fetch_wiki" then wikiRuntime.BuildPreludeForSession(sessionID, directory)
                        else Promise.lift (None: string option)
                    let final =
                        buildCapsMessages
                            VibeFs.Shell.FileSys.sha256HexTruncated
                            encoded
                            directory
                            capsFiles
                            wikiPrelude
                    replaceArrayInPlace messagesArr final
    }

let compactingHandlerFor (host: Host) (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let sessionID = Dyn.str input "sessionID"
        let backlog = magicSession.GetOrRebuildBacklog(sessionID, [])
        if backlog.IsEmpty then ()
        else
            let context = Dyn.get output "context"
            if not (Dyn.isNullish context) && Dyn.isArray context then
                let hint = "Preserve the latest " + magicTodoToolNameFor host + " result and the complete Magic Todo backlog in the summary. If earlier user messages are folded, rewrite them into that todo summary as work-period user updates instead of preserving raw user messages verbatim."
                (box context)?push(box hint) |> ignore
    }

let compactingHandler (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    compactingHandlerFor opencode magicSession input output
