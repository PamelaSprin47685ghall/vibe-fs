module VibeFs.Shell.MuxHookInputCodec

open VibeFs.Shell.Dyn

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

let isReadOnlyExecutorMux (tool: string) (args: obj) : bool =
    tool = "executor" && Dyn.str args "mode" = "ro"

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