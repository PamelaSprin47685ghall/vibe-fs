module VibeFs.Mux.Wrappers

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicTodo
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.TreeSitterKernel
open VibeFs.Opencode.HookSchema
open VibeFs.Shell.TreeSitterShell
open VibeFs.Shell.CallStore
open VibeFs.Shell.MagicSessionStore

type JsonSchema =
    { ``type``: string
      properties: obj
      required: string array option
      additionalProperties: bool option }

type ToolDefinition =
    { name: string
      description: string
      parameters: JsonSchema
      execute: obj -> obj -> JS.Promise<string>
      condition: (obj -> bool) option }

let resolveStr (s: string) : JS.Promise<string> = Promise.lift s

let jsonStringify (o: obj) : string = JS.JSON.stringify(o)

let optInt (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<int> v)
let optBool (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some(unbox<bool> v)
let optField (a: obj) (k: string) = let v = Dyn.get a k in if Dyn.isNullish v then None else Some v

let strField (a: obj) (k: string) : string option =
    let v = Dyn.get a k
    if Dyn.isNullish v then None else Some(string v)

let requireStrArray (a: obj) (k: string) : string array =
    let v = Dyn.get a k
    if Dyn.isNullish v || not (Dyn.isArray v) then [||]
    else v :?> obj array |> Array.map string

let mkSchema (props: obj) (required: string array) : JsonSchema =
    { ``type`` = "object"; properties = props; required = Some required; additionalProperties = Some false }

let strProp (desc: string) : obj = createObj [ "type", box "string"; "description", box desc ]
let numProp (desc: string) : obj = createObj [ "type", box "number"; "description", box desc ]
let boolProp (desc: string) : obj = createObj [ "type", box "boolean"; "description", box desc ]
let strEnumProp (desc: string) (values: string array) : obj = createObj [ "type", box "string"; "enum", box values; "description", box desc ]
let strArrayProp (desc: string) : obj =
    createObj [ "type", box "array"; "items", box (createObj [ "type", box "string" ]); "description", box desc ]

let requireWorkspaceId (config: obj) (toolName: string) : Result<string, string> =
    let wid = Dyn.get config "workspaceId"
    if isNull wid || string wid = "" then Result.Error $"{toolName} requires workspaceId"
    else Result.Ok(string wid)

let private applySyntaxCheck (result: obj) (args: obj) (config: obj) : JS.Promise<obj> =
    promise {
        match extractFilePath args with
        | None -> return result
        | Some filePath ->
            try
                let! formatted = readAndCheckSyntax filePath (Dyn.str config "cwd") false
                match formatted with
                | None -> return result
                | Some f ->
                    if Dyn.typeIs result "string" then return box (string result + "\n\n" + f)
                    elif Dyn.typeIs result "object" && Dyn.truthy (Dyn.get result "success") then
                        return Dyn.withKey result "syntax_diagnostics" (box f)
                    else return result
            with _ -> return result
    }

let private bindExecute (tool: obj) : obj = tool?execute

let private disabledResult () : JS.Promise<string> = Promise.lift "disabled"

let private appendMeditatorNudge (result: obj) : obj =
    if Dyn.isNullish result then result
    elif Dyn.typeIs result "string" then
        let s = string result
        if s.Contains(meditatorNudge : string) then result else box $"{s}\n\n{meditatorNudge}"
    elif Dyn.typeIs result "object" then
        let success = Dyn.get result "success"
        if not (Dyn.isNullish success) && unbox<bool> success then
            let existingNudge = Dyn.get result "nudge"
            if not (Dyn.isNullish existingNudge) && (string existingNudge).Contains(meditatorNudge : string) then result
            else Dyn.withKey result "nudge" (box meditatorNudge)
        else result
    else result

/// Encapsulates the host's native file_read execute function captured during
/// wrapper registration. Replaces the old `obj option ref` pseudo-interface
/// (REFACTOR.md §12): the mutable slot is private, callers go through methods.
type HostReadExec() =
    let mutable captured : obj option = None
    member _.Capture(fn: obj) : unit = captured <- Some fn
    member _.TryGet() : obj option = captured

let private reviewerAgentReportDefinition () : ToolDefinition =
    { name = "agent_report"
      description = "Submit a review verdict. Provide callId, verdict, and feedback; the wrapper forwards the verdict as the upstream agent_report markdown."
      parameters =
          { ``type`` = "object"
            properties =
                createObj
                    [ "callId", box (createObj [ "type", box "string"; "description", box "Internal review call id supplied by the prompt." ])
                      "verdict", box (createObj [ "type", box "string"; "enum", box [| "PASS"; "REJECT" |]; "description", box "PASS accepts the work; REJECT sends actionable feedback." ])
                      "feedback", box (createObj [ "type", box "string"; "description", box "Detailed actionable feedback. Empty string when passing." ]) ]
            required = Some [| "callId"; "verdict"; "feedback" |]
            additionalProperties = Some false }
      execute = fun _ _ -> resolveStr "" 
      condition = None }

let private formatReviewerAgentReportMarkdown (args: obj) : string =
    let verdict = defaultArg (strField args "verdict") "" |> fun value -> value.Trim().ToUpperInvariant()
    let feedback = defaultArg (strField args "feedback") "" |> fun value -> value.Trim()
    if verdict = "REJECT" || feedback <> "" then
        if feedback = "" then "REJECT: No feedback provided."
        else "REJECT: " + feedback
    else
        "PASS"

let private reviewerAgentReportPayload (args: obj) : obj =
    createObj [ "reportMarkdown", box (formatReviewerAgentReportMarkdown args) ]

let private isThenable (value: obj) : bool =
    not (Dyn.isNullish value) && Dyn.typeIs (Dyn.get value "then") "function"

let private mkResultWrapper (targetTool: string) (callback: obj -> obj -> obj -> JS.Promise<obj>) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun tool config ->
            let orig = tool?execute
            if not (Dyn.typeIs orig "function") then
                tool
            else
                let executeFn =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args opts ->
                        promise {
                            let raw = tool?execute(args, opts)
                            let! v =
                                if isThenable raw then unbox<JS.Promise<obj>> raw
                                else Promise.lift raw
                            return! callback v args config
                        })
                Dyn.withKey tool "execute" (box executeFn))
    createObj [ "targetTool", box targetTool; "wrapper", box wrapperFn ]

let private mkSyncResultWrapper (targetTool: string) (callback: obj -> obj) : obj =
    mkResultWrapper targetTool (fun result _ _ -> Promise.lift (callback result))

let private mkSyntaxWrappers () : obj array =
    [| mkResultWrapper "file_edit_replace_string" (fun result args config -> applySyntaxCheck result args config)
       mkResultWrapper "file_edit_insert" (fun result args config -> applySyntaxCheck result args config) |]

let private todoItemForNativeWrite (todo: obj) : obj =
    createObj [ "content", box (Dyn.str todo "content"); "status", box (Dyn.str todo "status") ]

let private todoArrayForNativeWrite (args: obj) : obj =
    let todos = Dyn.get args "todos"
    if Dyn.isNullish todos || not (Dyn.isArray todos) then
        box [||]
    else
        todos :?> obj array |> Array.map todoItemForNativeWrite |> box

let private captureTodoReport (args: obj) (opts: obj) : unit =
    let report = Dyn.str args "completedWorkReport" |> fun value -> value.Trim()
    let toolCallId = Dyn.str opts "toolCallId"
    if report <> "" && toolCallId <> "" then
        captureReport opencode toolCallId report

let private mkTodoWriteWrapper () : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (tool: obj) (_config: obj) ->
            let execFn =
                System.Func<obj, obj, JS.Promise<obj>>(fun (args: obj) (opts: obj) ->
                    promise {
                        captureTodoReport args opts
                        let nativeArgs = createObj [ "todos", todoArrayForNativeWrite args ]
                        let raw = tool?execute(nativeArgs, opts)
                        let! result =
                            if isThenable raw then unbox<JS.Promise<obj>> raw
                            else Promise.lift raw
                        return appendMeditatorNudge result
                    })

            createObj
                [ "description", box toolDescription
                  "parameters", buildMagicTodoSchema ()
                  "execute", box execFn ])

    createObj [ "targetTool", box (todoWritePromptName opencode); "wrapper", box wrapperFn ]

let private mkFileReadCapture (hostReadExec: HostReadExec) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (hostTool: obj) (_config: obj) ->
            hostReadExec.Capture(bindExecute hostTool)
            let execFn =
                System.Func<obj, obj, JS.Promise<string>>(fun (_args: obj) (_opts: obj) ->
                    disabledResult ())
            createObj [ "execute", box execFn ])
    createObj [ "targetTool", box "file_read"; "wrapper", box wrapperFn ]

let private mkAgentReportOverride (callStore: CallStore) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun (tool: obj) (config: obj) ->
            if Dyn.str config "subagentRole" <> "reviewer" then
                tool
            else
                let definition = reviewerAgentReportDefinition ()
                let execFn =
                    System.Func<obj, obj, JS.Promise<obj>>(fun (args: obj) (opts: obj) ->
                        promise {
                            let callId = Dyn.str args "callId"
                            if callId <> "" then
                                resolveCall callStore callId args |> ignore

                            let upstreamArgs = reviewerAgentReportPayload args
                            let raw = tool?execute(upstreamArgs, opts)
                            let! result = if isThenable raw then unbox<JS.Promise<obj>> raw else Promise.lift raw
                            if Dyn.typeIs result "object" && Dyn.truthy (Dyn.get result "success") then
                                return Dyn.withKey result "report" (box upstreamArgs)
                            else
                                return result
                        })
                createObj [ "description", box definition.description
                            "parameters", box definition.parameters
                            "execute", box execFn ])
    createObj [ "targetTool", box "agent_report"; "wrapper", box wrapperFn ]


let createAllWrappersFor (host: Host) (tools: obj) (hostReadExec: HostReadExec) (callStore: CallStore) : obj array =
    Array.append
        (mkSyntaxWrappers ())
        [| mkFileReadCapture hostReadExec
           mkTodoWriteWrapper ()
           mkAgentReportOverride callStore |]

let createAllWrappers (tools: obj) (hostReadExec: HostReadExec) (callStore: CallStore) : obj array =
    createAllWrappersFor opencode tools hostReadExec callStore
