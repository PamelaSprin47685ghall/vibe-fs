namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

module PluginTools =

    let buildToolsObject (rt: PluginRuntime) =
        let executeTool (tool: Tool) (args: obj) (context: obj) : Task<obj> =
            task {
                let sIdStr =
                    if not (isNull context) && not (isNull context?sessionID) then unbox<string> context?sessionID
                    elif not (isNull context) && not (isNull context?sessionId) then unbox<string> context?sessionId
                    else ""
                let sId = SessionId.create sIdStr
                let sr = rt.EnsureSessionDriver sId
                let port = SessionInboxCommandPort sr.Inbox
                let payloadStr = Fable.Core.JS.JSON.stringify args
                let ctx: ToolContext =
                    { SessionId = sId
                      Workspace = rt.Directory
                      Cancellation = rt.CancellationToken
                      Deadline = Deadline.ofBudget DateTimeOffset.UtcNow (TimeSpan.FromSeconds 30.0)
                      Session = port }
                let! out = tool.Execute ctx { Payload = payloadStr }
                return box {| result = out.Result; output = out.Result |}
            }

        let makeToolObj (tool: Tool) =
            {| description = tool.Description
               parameters = Fable.Core.JS.JSON.parse tool.SchemaJson
               execute = fun (args: obj) (context: obj) -> executeTool tool args context |}

        let dummyPort = SessionInboxCommandPort(FifoInbox(10) :> ISessionInbox)
        let todoT = StaticTools.todowriteTool dummyPort
        let execT = StaticTools.executorTool ()
        let readT = FileTools.fileReadTool ()
        let writeT = FileTools.fileWriteTool ()
        let editT = FileTools.fileEditTool ()

        createObj
            [ "todowrite", box (makeToolObj todoT)
              "executor", box (makeToolObj execT)
              "read", box (makeToolObj readT)
              "write", box (makeToolObj writeT)
              "edit", box (makeToolObj editT) ]
