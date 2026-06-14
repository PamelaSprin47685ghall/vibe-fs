module VibeFs.Opencode.Hooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

let private defaultExcludedAgents = [ "browser"; "greper"; "executor"; "title" ]

[<Emit("{}")>]
let private emptyObj () : obj = jsNative
[<Emit("$0[$1] = $2")>]
let private setKey (o: obj) (k: string) (v: obj) : unit = jsNative
[<Emit("$0.output = $1")>]
let private setOutput (o: obj) (v: string) : unit = jsNative
[<Emit("Promise.resolve()")>]
let private resolvedUnit : JS.Promise<unit> = jsNative

[<Emit("Object.keys($0)")>]
let private objectKeys (o: obj) : string array = jsNative

[<Emit("$0.splice(0, $0.length, ...$1)")>]
let private replaceArrayInPlace (target: obj array) (source: obj array) : unit = jsNative

[<Emit("typeof $0")>]
let private jsTypeof (o: obj) : string = jsNative

let private isStringArray (value: obj) : bool =
    Dyn.isArray value && (value :?> obj array) |> Array.forall (fun x -> Dyn.typeIs x "string")

let private joinGreperIntents (intents: obj) : Result<string, string> =
    if not (Dyn.isArray intents) || not (isStringArray intents) then
        Error "Invalid LLM input for greper: intents must be an array of strings"
    else
        Ok (String.concat "; " ((intents :?> obj array) |> Array.map string))

let private joinEditorIntents (intents: obj) : Result<string, string> =
    if not (Dyn.isArray intents) then
        Error "Invalid LLM input for editor: intents must be an array"
    else
        let firstItems =
            (intents :?> obj array)
            |> Array.map (fun intent ->
                if Dyn.isArray intent then
                    let arr = intent :?> obj array
                    if Array.isEmpty arr then box null else arr.[0]
                else
                    intent)
        if not (firstItems |> Array.forall (fun x -> Dyn.typeIs x "string")) then
            Error "Invalid LLM input for editor: each intent must start with a string"
        else
            Ok (String.concat "; " (firstItems |> Array.map string))

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
                box ((required :?> string array) |> Array.filter (fun k -> k <> "_ui"))
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

/// Merge user tool overrides onto defaults, keeping only boolean overrides.
let private mergeTools (current: obj) (defaults: obj) : obj =
    let merged = emptyObj ()
    for key in objectKeys defaults do
        setKey merged key (Dyn.get defaults key)
    if not (Dyn.isNullish current) then
        for key in objectKeys current do
            let value = Dyn.get current key
            if Dyn.typeIs value "boolean" then
                setKey merged key value
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
let chatMessage (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        let agent = resolveAgent input
        let message = Dyn.get output "message"
        if not (Dyn.isNullish message) then
            let tools = Dyn.get message "tools"
            match resolveChatTools agent tools with
            | Some next -> setKey message "tools" next
            | None -> ()
    } |> Async.StartAsPromise

/// tool.execute.after: append the reverie nudge to todowrite output.
let toolExecuteAfter (input: obj) (output: obj) : JS.Promise<unit> =
    async {
        if Dyn.str input "tool" <> "todowrite" then ()
        else
            let out = Dyn.get output "output"
            if not (Dyn.isNullish out) && Dyn.typeIs out "string" then
                let s = string out
                if not (s.Contains Prompts.reverieNudge) then setOutput output (s + "\n" + Prompts.reverieNudge)
    } |> Async.StartAsPromise

/// Deduplicate repeated `read` tool outputs across messages to reduce token use.
let private applyReadDedup (messages: obj array) : obj array =
    if Dyn.isNullish messages || not (Dyn.isArray messages) then
        messages
    else
        let mutable messagesChanged = false
        let mutable nextMessages = messages
        let mutable seen : string list = []

        for i = 0 to messages.Length - 1 do
            let message = messages.[i]
            if not (Dyn.isNullish message) then
                let parts = Dyn.get message "parts"
                if not (Dyn.isNullish parts) && Dyn.isArray parts then
                    let partsArr = parts :?> obj array
                    let mutable partsChanged = false
                    let mutable nextParts = partsArr

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
                                    let result = Dedup.deduplicate seen currentOutput
                                    seen <- result.seenOutputs
                                    if result.output <> currentOutput then
                                        if not partsChanged then
                                            nextParts <- Array.copy partsArr
                                            partsChanged <- true
                                        if not messagesChanged then
                                            nextMessages <- Array.copy messages
                                            messagesChanged <- true
                                        let nextState = Dyn.withKey state "output" (box result.output)
                                        let nextPart = Dyn.withKey part "state" (box nextState)
                                        nextParts.[j] <- nextPart

                    if partsChanged then
                        let nextMessage = Dyn.withKey message "parts" (box nextParts)
                        nextMessages.[i] <- nextMessage

        nextMessages

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
            let deduped = applyReadDedup next
            replaceArrayInPlace messagesArr deduped
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
