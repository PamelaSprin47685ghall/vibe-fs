module Wanxiangshu.Tests.IntegrationMuxSetup

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn


let muxToolConfig (directory: string) (sessionID: string) : obj =
    createObj
        [ "directory", box directory
          "sessionID", box sessionID
          "workspaceId", box sessionID ]

let muxToolByName (reg: obj) (name: string) : obj =
    let tools = unbox<obj[]> (get reg "tools")
    tools
    |> Array.tryFind (fun t -> str t "name" = name)
    |> Option.defaultValue null

let muxToolSchema (toolDef: obj) : obj =
    if isNullish toolDef then null else get toolDef "parameters"

let muxToolSchemaRequired (toolDef: obj) : string array =
    if isNullish toolDef then [||]
    else
        let schema = muxToolSchema toolDef
        if isNullish schema then [||]
        else
            let req = get schema "required"
            if isArray req then unbox<string[]> req else [||]

let muxExecutorModeSchema (reg: obj) : obj =
    let executor = muxToolByName reg "executor"
    let schema = muxToolSchema executor
    if isNullish schema then null
    else
        let props = get schema "properties"
        if isNullish props then null else get props "mode"

let muxKnowledgeGraphRuntime (reg: obj) : obj =
    let direct = get reg "__knowledgeGraphRuntime"
    if not (isNullish direct) then direct
    else
        let rt = get reg "knowledgeGraphRuntime"
        if isNullish rt then null else rt

let muxReviewStore (reg: obj) : obj = get reg "__reviewStore"

let muxActivateReviewForTest (reg: obj) (sessionID: string) (task: string) : unit =
    let store = muxReviewStore reg
    let activate = get store "activateReview" |> unbox<System.Func<string, string, int64, unit>>
    activate.Invoke(sessionID, task, 0L)

let muxIsReviewActiveForTest (reg: obj) (sessionID: string) : bool =
    let store = muxReviewStore reg
    let fn = get store "isReviewActive" |> unbox<System.Func<string, bool>>
    fn.Invoke(sessionID)

let minimalMuxDeps () : obj =
    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj []))) ]

let muxDepsWithChatHistory (sessionID: string) (messages: obj array) : obj =
    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
          "getChatHistory",
          box (System.Func<string, JS.Promise<obj array>>(fun sid ->
              promise { return if sid = sessionID then messages else [||] })) ]

let muxMutableDepsWithChatHistory (sessionID: string) (messages: ResizeArray<obj>) : obj =
    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
          "getChatHistory",
          box (System.Func<string, JS.Promise<obj array>>(fun sid ->
              promise { return if sid = sessionID then messages.ToArray() else [||] })) ]

let mockMuxTaskServiceCapturingPrompt (prompts: ResizeArray<string>) : obj =
    createObj
        [ "create",
          box (System.Func<obj, JS.Promise<obj>>(fun input ->
              promise {
                  let promptText = str input "prompt"
                  if promptText <> "" then prompts.Add(promptText)
                  return box {| success = true; data = box {| taskId = "reviewer-task-1"; kind = "agent" |} |}
              }))
          "waitForAgentReport",
          box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
              Promise.reject (exn "simulated reviewer timeout"))) ]

/// Mock task service whose reviewer rounds return queued `reportMarkdown`
/// verdicts in order. The parent submit_review flow consumes one report per
/// delegate round (round1, then round2 double-check when round1 passes), so the
/// queue length matches the number of expected reviewer rounds.
let mockMuxTaskServiceReturningVerdicts (prompts: ResizeArray<string>) (verdicts: string list) : obj =
    let queue = ResizeArray<string>(verdicts)
    createObj
        [ "create",
          box (System.Func<obj, JS.Promise<obj>>(fun input ->
              promise {
                  let promptText = str input "prompt"
                  if promptText <> "" then prompts.Add(promptText)
                  return box {| success = true; data = box {| taskId = "reviewer-task-1"; kind = "agent" |} |}
              }))
          "waitForAgentReport",
          box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
              promise {
                  let report = if queue.Count > 0 then let v = queue.[0] in queue.RemoveAt(0); v else ""
                  return box {| reportMarkdown = report |}
              })) ]

let registerMuxKnowledgeGraphJobForTest (reg: obj) (sessionID: string) (workspaceRoot: string) (kindTag: string) (payload: obj) : unit =
    let runtime = muxKnowledgeGraphRuntime reg
    let registrar = get runtime "registerJobForTesting" |> unbox<System.Func<string, string, string, obj, unit>>
    registrar.Invoke(sessionID, workspaceRoot, kindTag, payload)

let muxMessageTransform (reg: obj) : obj =
    get reg "messagesTransform"

let muxTextMessage (id: string) (role: string) (text: string) : obj =
    box {| id = id; role = role; parts = [| box {| ``type`` = "text"; text = text; state = "done" |} |] |}

let firstTextPartText (msg: obj) : string =
    let parts = get msg "parts"
    if isNullish parts then ""
    else
        let arr = unbox<obj[]> parts
        if arr.Length = 0 then ""
        else str arr.[0] "text"

let hasDynamicToolReadPart (msg: obj) : bool =
    let parts = get msg "parts"
    if isNullish parts then false
    else
        unbox<obj[]> parts
        |> Array.exists (fun p ->
            str p "type" = "dynamic-tool"
            && str p "toolName" = "file_read")

let muxDynamicToolMessage (id: string) (toolName: string) (toolCallId: string) (input: obj) (output: obj) : obj =
    box
        {| id = id
           role = "assistant"
           parts =
            [| box
                   {| ``type`` = "dynamic-tool"
                      toolName = toolName
                      toolCallId = toolCallId
                      state = "output-available"
                      input = input
                      output = output |} |] |}

let muxFirstDynamicToolOutput (msg: obj) : obj =
    get (unbox<obj[]> (get msg "parts")).[0] "output"
