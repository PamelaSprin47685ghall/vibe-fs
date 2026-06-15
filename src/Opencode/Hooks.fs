module VibeFs.Opencode.Hooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Shell.TreeSitterShell

let private defaultExcludedAgents = [ "browser"; "greper"; "summarizer"; "title" ]

let private emptyObj () : obj = createObj []
let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v
let private setOutput (o: obj) (v: string) : unit = o?output <- v
let private resolvedUnit : JS.Promise<unit> = async { return () } |> Async.StartAsPromise

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys(o) |> Seq.toArray

let private replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if obj.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore

let private jsTypeof (o: obj) : string = Dyn.jsType o

let private isStringArray (value: obj) : bool =
    if not (Dyn.isArray value) then false
    else
        let items: obj array = unbox value
        let mutable allStrings = true
        let mutable index = 0
        while allStrings && index < items.Length do
            allStrings <- Dyn.typeIs items.[index] "string"
            index <- index + 1
        allStrings

let private joinGreperIntents (intents: obj) : Result<string, string> =
    if not (Dyn.isArray intents) || not (isStringArray intents) then
        Error "Invalid LLM input for greper: intents must be an array of strings"
    else
        let items: obj array = unbox intents
        let texts = Array.zeroCreate<string> items.Length
        for index = 0 to items.Length - 1 do
            texts.[index] <- string items.[index]
        Ok (String.concat "; " texts)

let private joinEditorIntents (intents: obj) : Result<string, string> =
    if not (Dyn.isArray intents) then
        Error "Invalid LLM input for editor: intents must be an array"
    else
        let items: obj array = unbox intents
        let firstItems = Array.zeroCreate<string> items.Length
        let mutable invalid = false
        let mutable index = 0
        while not invalid && index < items.Length do
            let firstItem = Dyn.get items.[index] "0"
            if not (Dyn.typeIs firstItem "string") then invalid <- true
            else firstItems.[index] <- string firstItem
            index <- index + 1
        if invalid then
            Error "Invalid LLM input for editor: each intent must start with a string"
        else
            Ok (String.concat "; " firstItems)

let private stripUiParameter (parameters: obj) : unit =
    let properties = Dyn.get parameters "properties"
    if Dyn.isNullish properties then ()
    else
        let nextProps = emptyObj ()
        for key in objectKeys properties do
            if key <> "_ui" then setKey nextProps key (Dyn.get properties key)
        let required = Dyn.get parameters "required"
        let nextRequired =
            if Dyn.isArray required then
                let requiredKeys: obj array = unbox required
                let kept = ResizeArray<string>()
                for keyObj in requiredKeys do
                    let key = string keyObj
                    if key <> "_ui" then kept.Add key
                box (kept.ToArray())
            else
                required
        setKey parameters "properties" nextProps
        setKey parameters "required" nextRequired

/// Resolve the effective agent for a chat message: explicit agent, registered
/// child session, or orchestrator.
let private resolveAgent (input: obj) : string =
    let explicit = Dyn.str input "agent"
    if explicit <> "" then explicit
    else
        match ChildAgent.lookupChildAgent (Dyn.str input "sessionID") with
        | Some a -> a
        | None -> "orchestrator"

/// Merge user tool overrides onto defaults. Defaults are authoritative: an
/// explicit false in current can disable a default-true tool, but an explicit
/// true cannot enable a tool that defaults to false. Extra keys in current
/// (dynamic tools such as stealth-browser-mcp_foo) are preserved only when
/// their value is a boolean.
let private mergeTools (current: obj) (defaults: obj) : obj =
    let merged = emptyObj ()
    for key in objectKeys defaults do
        setKey merged key (Dyn.get defaults key)
    if not (Dyn.isNullish current) then
        for key in objectKeys current do
            let currentValue = Dyn.get current key
            let defaultValue = Dyn.get defaults key
            if Dyn.isNullish defaultValue then
                if Dyn.typeIs currentValue "boolean" then
                    setKey merged key currentValue
            else
                if Dyn.typeIs defaultValue "boolean" && Dyn.typeIs currentValue "boolean" then
                    if defaultValue :?> bool then
                        setKey merged key currentValue
                    elif not (currentValue :?> bool) then
                        setKey merged key (box false)
    merged

/// For non-browser agents, strip every stealth-browser-mcp tool.
let private applyStealthBrowserRestrictions (tools: obj) (agent: string) : obj =
    if agent = "browser" then tools
    else
        let next = emptyObj ()
        for key in objectKeys tools do
            if key.StartsWith("stealth-browser-mcp_") then
                setKey next key (box false)
            else
                setKey next key (Dyn.get tools key)
        setKey next "stealth-browser-mcp_*" (box false)
        next

/// Re-apply an agent's tool defaults and enforce browser-MCP boundaries.
let private resolveChatTools (agent: string) (existingTools: obj) : obj option =
    match AgentRole.ofString agent with
    | Error _ -> None
    | Ok role ->
        let defaults = AgentConfig.toolDefaults role
        let merged = mergeTools existingTools defaults
        Some (applyStealthBrowserRestrictions merged agent)

/// chat.message: enforce per-agent tool boundaries at runtime.
let chatMessage (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let agent = resolveAgent input
        let sessionID = Dyn.str input "sessionID"
        do! nudgeHook.handleChatMessage(sessionID, agent, Dyn.get output "parts") |> Async.AwaitPromise
        let message = Dyn.get output "message"
        if not (Dyn.isNullish message) then
            let tools = Dyn.get message "tools"
            match resolveChatTools agent tools with
            | Some next -> setKey message "tools" next
            | None -> ()
    } |> Async.StartAsPromise

/// tool.execute.after: syntax-check file edits and delegate to the nudge hook.
let toolExecuteAfter (directory: string) (nudgeHook: VibeFs.Opencode.NudgeHook.NudgeHook) (input: obj) (output: obj) : JS.Promise<unit> =
    async {
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

open VibeFs.Kernel.Dedup

/// Deduplicate repeated `read` tool outputs across messages to reduce token use.
/// Mutates `state.output` in place; never swaps part/state/array references.
/// The opencode host keys internal bookkeeping off those references and ignores
/// replacements, so building a new object chain (Dyn.withKey / Array.copy)
/// silently no-ops.
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

/// messages.transform: synthesise a user+assistant read pair from CAPS files.
/// Mutates output.messages in place so the host never sees a swapped array reference.
let messagesTransform (directory: string) (output: obj) : JS.Promise<unit> =
    async {
        let messages = Dyn.get output "messages"
        if not (Dyn.isNullish messages) && Dyn.isArray messages then
            let messagesArr = messages :?> obj array
            let! capsFiles = VibeFs.Shell.CapsShell.findCapsFiles directory |> Async.AwaitPromise
            let next =
                VibeFs.Kernel.CapsFormat.buildCapsMessages
                    messagesArr
                    directory
                    defaultExcludedAgents
                    capsFiles
            replaceArrayInPlace messagesArr next
            applyReadDedup messagesArr
    } |> Async.StartAsPromise

/// tool.definition: hide the internal `_ui` parameter from editor/greper schemas.
let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let toolID = Dyn.str input "toolID"
        if toolID = "editor" || toolID = "greper" then
            let parameters = Dyn.get output "parameters"
            if not (Dyn.isNullish parameters) then stripUiParameter parameters
    } |> Async.StartAsPromise

/// tool.execute.before: populate `_ui` from joined intents so the UI labels the call.
let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let args = Dyn.get output "args"
        if Dyn.isNullish args then ()
        else
            let tool = Dyn.str input "tool"
            let existingUi = Dyn.get args "_ui"
            if not (Dyn.isNullish existingUi) && not (Dyn.typeIs existingUi "string") then
                setKey args "_ui" (box $"Invalid LLM input for {tool}: _ui must be a string, received {jsTypeof existingUi}")
            elif tool = "editor" then
                match joinEditorIntents (Dyn.get args "intents") with
                | Error e -> setKey args "_ui" (box e)
                | Ok ui -> setKey args "_ui" (box ui)
            elif tool = "greper" then
                match joinGreperIntents (Dyn.get args "intents") with
                | Error e -> setKey args "_ui" (box e)
                | Ok ui -> setKey args "_ui" (box ui)
    } |> Async.StartAsPromise

/// event: deactivate review on stream-abort.
let eventHandler (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) (input: obj) : JS.Promise<unit> =
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
