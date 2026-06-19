module VibeFs.Opencode.Hooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Kernel.Message
open VibeFs.Opencode.HookSchema
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicProjection
open VibeFs.Kernel.MagicTodo
open VibeFs.Opencode.MagicTodo
open VibeFs.Opencode.Actors
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.NudgeState
open VibeFs.Shell.TreeSitterShell

let private defaultExcludedAgents = [ "browser"; "investigator"; "executor"; "title" ]

let private emptyObj () : obj = createObj []

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private setOutput (o: obj) (v: string) : unit = o?output <- v
let private resolvedUnit : JS.Promise<unit> = async { return () } |> Async.StartAsPromise

let private replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if System.Object.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private jsTypeof (o: obj) : string = Dyn.jsType o

let private resolveAgent (registry: ChildAgentRegistry) (input: obj) : string =
    let explicit = Dyn.str input "agent"
    if explicit <> "" then explicit
    else
        match registry.LookupChildAgent(Dyn.str input "sessionID") with
        | Some a -> a
        | None -> "manager"

let private messageId (msg: obj) : string =
    let info = messageInfo msg
    if Dyn.isNullish info then "" else infoId info

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
    async {
        let agent = resolveAgent registry input
        let sessionID = Id.sessionIdQuick (Dyn.str input "sessionID")
        do! nudgeHook.handleChatMessage(sessionID, agent, Dyn.get output "parts") |> Async.AwaitPromise
        let message = Dyn.get output "message"
        if not (Dyn.isNullish message) then
            let tools = Dyn.get message "tools"
            if not (Dyn.isNullish tools) then
                setKey message "tools" (resolveChatTools host agent tools)
    } |> Async.StartAsPromise

let chatMessage (registry: ChildAgentRegistry) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    chatMessageFor opencode registry nudgeHook input output

let private stripMimocodeTaskArgsForExecute (input: obj) (args: obj) =
    let tool = normalizeToolName Mimocode (Dyn.str input "tool")
    if tool <> "todowrite" then ()
    else
        let callID = Dyn.str input "callID"
        let operation = Dyn.get args "operation"
        let topReport = Dyn.str args "completedWorkReport"
        let nestedReport = Dyn.str operation "completedWorkReport"
        let report = if topReport <> "" then topReport else nestedReport
        if report <> "" then captureCompletedWorkReport callID report
        // Must delete in place on the shared args/operation objects, never reassign output.args:
        // the host's wrapped execute re-parses the ORIGINAL args reference against the strict
        // task schema, so a replacement object is ignored and stray keys survive into validation
        // (unrecognized_keys at path []).
        Dyn.deleteKey args "completedWorkReport"
        Dyn.deleteKey args "task_id"
        Dyn.deleteKey operation "completedWorkReport"

let private rewriteMimocodeApplyPatchArgsForExecute (output: obj) (input: obj) (args: obj) =
    if Dyn.str input "tool" <> "apply_patch" then ()
    elif Dyn.typeIs args "string" then
        setKey output "args" (createObj [ "patchText", args ])
    else
        let patchText = Dyn.str args "patchText"
        if patchText <> "" then ()
        else
            let patch = Dyn.str args "patch"
            if patch <> "" then
                setKey output "args" (createObj [ "patchText", box patch ])
            else
                let text = Dyn.str args "text"
                if text <> "" then setKey output "args" (createObj [ "patchText", box text ])

let private restoreMimocodeTaskArgsAfterExecute (host: Host) (input: obj) (args: obj) =
    if host <> Mimocode then ()
    else
        let tool = normalizeToolName host (Dyn.str input "tool")
        if tool <> "todowrite" then ()
        else
            let callID = Dyn.str input "callID"
            let cached = takeCompletedWorkReport callID
            if cached <> "" then setKey args "completedWorkReport" (box cached)

let toolExecuteAfterFor (host: Host) (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let args = Dyn.get input "args"
        if not (Dyn.isNullish args) then restoreMimocodeTaskArgsAfterExecute host input args
        let tool = Dyn.str input "tool"
        if isFileEditTool tool then
            let out = Dyn.get output "output"
            if not (Dyn.isNullish out) && Dyn.typeIs out "string" then
                let s = string out
                if not (hasSyntaxCheckMarker s) then
                    let args = Dyn.get input "args"
                    let paths = extractFilePaths args
                    let! diagnostics =
                        paths
                        |> List.map (fun path -> readAndCheckSyntax path directory false |> Async.AwaitPromise)
                        |> Async.Parallel
                    let formatted =
                        let lines = ResizeArray<string>()
                        for diagnostic in diagnostics do
                            match diagnostic with
                            | Some text -> lines.Add text
                            | None -> ()
                        String.concat "\n" lines
                    if formatted <> "" then setOutput output (s + "\n\n" + formatted)
        do! nudgeHook.handleToolExecuteAfter input output |> Async.AwaitPromise
    } |> Async.StartAsPromise

let toolExecuteAfter (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteAfterFor opencode directory nudgeHook input output

open VibeFs.Kernel.Dedup

let private applyReadDedup (messages: obj array) : unit =
    if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
    else
        let seenByPath = emptyObj ()
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
        | true, files -> async { return files } |> Async.StartAsPromise
        | false, _ ->
            async {
                let! files = VibeFs.Shell.WorkspaceFiles.findCapsFiles directory |> Async.AwaitPromise
                if not (cache.ContainsKey sessionID) then cache.[sessionID] <- files
                return files
            } |> Async.StartAsPromise

let messagesTransform (registry: ChildAgentRegistry) (directory: string) (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let messages = Dyn.get output "messages"
        if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
        else
            let messagesArr = messages :?> obj array
            if messagesArr.Length = 0 then ()
            else
                let agent = resolveAgent registry input
                let sessionID = extractSessionID messagesArr
                let cleaned = stripSyntheticMessages messagesArr
                if cleaned.Length = 0 then ()
                else
                    if defaultExcludedAgents |> List.contains agent then
                        replaceArrayInPlace messagesArr cleaned
                    else
                        let backlog = magicSession.GetOrRebuildBacklog(sessionID, cleaned)
                        let afterMagic = projectMagicFor magicSession.Host cleaned backlog false sessionID
                        applyReadDedup afterMagic
                        let! capsFiles = CapsFileCache.getOrLoad sessionID directory |> Async.AwaitPromise
                        let final =
                            buildCapsMessages
                                VibeFs.Shell.FileSys.sha256HexTruncated
                                afterMagic
                                directory
                                defaultExcludedAgents
                                capsFiles
                        replaceArrayInPlace messagesArr final
    } |> Async.StartAsPromise

let compactingHandlerFor (host: Host) (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let sessionID = Dyn.str input "sessionID"
        let backlog = magicSession.GetOrRebuildBacklog(sessionID, [||])
        if backlog.IsEmpty then ()
        else
            let context = Dyn.get output "context"
            if not (Dyn.isNullish context) && Dyn.isArray context then
                let hint = "Preserve the latest " + magicTodoToolNameFor host + " result and the complete Magic Todo backlog in the summary. If earlier user messages are folded, rewrite them into that todo summary as work-period user updates instead of preserving raw user messages verbatim."
                (box context)?push(box hint) |> ignore
    } |> Async.StartAsPromise

let compactingHandler (magicSession: MagicSession) (input: obj) (output: obj) : JS.Promise<unit> =
    compactingHandlerFor opencode magicSession input output

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
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
    } |> Async.StartAsPromise

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    toolDefinitionFor opencode input output

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let args = Dyn.get output "args"
        if Dyn.isNullish args then ()
        else
            let tool = Dyn.str input "tool"
            setUiLabel setKey args tool
            if host = Mimocode then
                stripMimocodeTaskArgsForExecute input args
                rewriteMimocodeApplyPatchArgsForExecute output input args
    } |> Async.StartAsPromise

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteBeforeFor opencode input output

let eventHandler (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (input: obj) : JS.Promise<unit> =
    async {
        let event = Dyn.get input "event"
        if Dyn.str event "type" = "stream-abort" then
            let props = Dyn.get event "properties"
            let sessionID =
                if Dyn.isNullish props then "loop"
                else
                    let s = Dyn.str props "sessionID"
                    if s = "" then "loop" else s
            reviewStore.deactivateReview sessionID
    } |> Async.StartAsPromise

let noop (_a: obj) (_b: obj) : JS.Promise<unit> = resolvedUnit
let noopEvent (_a: obj) : JS.Promise<unit> = resolvedUnit
