module VibeFs.Shell.MuxHookInputCodec

open Fable.Core.JsInterop
open VibeFs.Shell.Dyn
open VibeFs.Shell.ExecutorToolsCodec

type MuxMessagesTransformInput =
    { Agent: string
      SessionID: string
      Directory: string }

let decodeMuxMessagesTransformInput (input: obj) (deps: obj) : MuxMessagesTransformInput =
    let agent =
        let explicit = Dyn.str input "agent"
        if explicit <> "" then explicit else Dyn.str input "effectiveAgentId"
    let sessionID =
        let explicit = Dyn.str input "sessionID"
        if explicit <> "" then explicit else Dyn.str input "workspaceId"
    let directory =
        let explicit = Dyn.str input "directory"
        if explicit <> "" then explicit
        else
            let fromPath = Dyn.str input "workspacePath"
            if fromPath <> "" then fromPath else Dyn.str deps "directory"
    { Agent = agent; SessionID = sessionID; Directory = directory }

type MuxToolExecuteAfterInput =
    { Tool: string
      SessionID: string
      Directory: string
      WorkspaceId: string
      Args: obj }

let argsFromMuxToolExecuteInput (input: obj) : obj = Dyn.get input "args"

let hookOutputErrorMux (output: obj) : string = Dyn.str output "error"

let hookOutputTextMux (output: obj) : string = Dyn.str output "output"

let setHookOutputStringMux (output: obj) (text: string) : unit = output?("output") <- text

/// Read output.args (tool execute args rewriter payload); absent yields `obj` sentinel.
let argsFromHookOutputMux (output: obj) : obj = Dyn.get output "args"

/// Write output.args — host wire SSOT for tool execute args rewriter (Mux).
let setHookArgsMux (output: obj) (args: obj) : unit = output?("args") <- args

/// Write output.error — host wire SSOT for tool execute error payload (Mux).
let setHookErrorMux (output: obj) (error: string) : unit = output?("error") <- box error

/// Read output.error optional string (absent → None, present non-string → None).
let hookOutputErrorOptMux (output: obj) : string option =
    let raw = Dyn.get output "error"
    if Dyn.isNullish raw || not (Dyn.typeIs raw "string") then None
    else Some (unbox<string> raw)

let isReadOnlyExecutorMux (tool: string) (args: obj) : bool =
    tool = "executor"
    && match peekExecutorMode args with
       | Some mode -> System.String.Equals(mode, "ro", System.StringComparison.OrdinalIgnoreCase)
       | None -> false

let decodeMuxToolExecuteAfterInput (input: obj) (deps: obj) : MuxToolExecuteAfterInput =
    let tool = Dyn.str input "tool"
    let sessionID = Dyn.str input "sessionID"
    let directory =
        let d = Dyn.str input "directory"
        if d <> "" then d else Dyn.str deps "directory"
    let workspaceId =
        let w = Dyn.str input "workspaceId"
        if w <> "" then w
        elif sessionID <> "" then sessionID
        else Dyn.str deps "workspaceId"
    { Tool = tool
      SessionID = sessionID
      Directory = directory
      WorkspaceId = workspaceId
      Args = argsFromMuxToolExecuteInput input }
