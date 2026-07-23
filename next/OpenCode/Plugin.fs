namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools

type PluginConfig = { Directory: string }

module Plugin =

    let private buildHookInput (inObj: obj) : OpencodeHookInput =
        let m =
            if isNull inObj || isNull inObj?model then
                None
            else
                let mObj = inObj?model

                Some
                    { providerID = unbox<string> mObj?providerID
                      modelID = unbox<string> mObj?modelID
                      variant =
                        if isNull mObj?variant then
                            None
                        else
                            Some(unbox<string> mObj?variant) }

        { sessionID =
            if isNull inObj || isNull inObj?sessionID then
                ""
            else
                unbox<string> inObj?sessionID
          messageID =
            if isNull inObj || isNull inObj?messageID then
                None
            else
                Some(unbox<string> inObj?messageID)
          agent =
            if isNull inObj || isNull inObj?agent then
                None
            else
                Some(unbox<string> inObj?agent)
          model = m }

    let private handleChatTransform (rt: PluginRuntime) (outObj: obj) =
        if not (isNull outObj) then
            let proj = rt.Gateway.ProjectionSet
            let caps = [ "coder"; "inspector"; "browser"; "meditator" ]
            let revCtx = proj.LastReview |> Option.map (fun v -> sprintf "%A" v)

            let snap: SessionSnapshot =
                { Caps = caps
                  ReviewContext = revCtx
                  ParallelHint = Some "Issue independent tool calls in parallel when possible." }

            let rawMsgs =
                if isNull outObj?messages then
                    []
                else
                    unbox<obj list> outObj?messages

            let hostMsgs =
                rawMsgs
                |> List.choose (fun m ->
                    if isNull m then
                        None
                    else
                        let r = if isNull m?role then "" else unbox<string> m?role
                        let t = if isNull m?text then "" else unbox<string> m?text

                        Some
                            { Role = r
                              Text = t
                              ToolCalls = None
                              Metadata = None })

            let transformed = MessageTransform.transform snap hostMsgs

            let jsMsgs =
                transformed |> List.map (fun tm -> {| role = tm.Role; text = tm.Text |} |> box)

            outObj?messages <- jsMsgs

    let private handleToolDefinition (outObj: obj) =
        if not (isNull outObj) then
            let staticToolsList =
                [ {| name = "todowrite"
                     description = "Update task todo snapshot, report progress, and methodology."
                     parameters = Fable.Core.JS.JSON.parse """{"type":"object","properties":{"todos":{"type":"array","items":{"type":"string"}}},"required":["todos"]}""" |}
                  {| name = "read"
                     description = "Read file content from filesystem."
                     parameters = Fable.Core.JS.JSON.parse """{"type":"object","properties":{"filePath":{"type":"string"}},"required":["filePath"]}""" |}
                  {| name = "write"
                     description = "Write file content to filesystem."
                     parameters = Fable.Core.JS.JSON.parse """{"type":"object","properties":{"filePath":{"type":"string"},"content":{"type":"string"}},"required":["filePath","content"]}""" |}
                  {| name = "edit"
                     description = "Edit file content in filesystem using exact string replacement."
                     parameters = Fable.Core.JS.JSON.parse """{"type":"object","properties":{"filePath":{"type":"string"},"oldString":{"type":"string"},"newString":{"type":"string"}},"required":["filePath","oldString","newString"]}""" |}
                  {| name = "executor"
                     description = "Execute shell command within timeout budget."
                     parameters = Fable.Core.JS.JSON.parse """{"type":"object","properties":{"command":{"type":"string"}},"required":["command"]}""" |} ]
                |> List.map box
                |> List.toArray

            outObj?tools <- staticToolsList

    let private handleToolExecuteAfter (rt: PluginRuntime) (inObj: obj) (outObj: obj) =
        if not (isNull inObj) then
            let t =
                if isNull inObj?tool then
                    "unknown"
                else
                    unbox<string> inObj?tool

            let s =
                if isNull inObj?sessionID then
                    ""
                else
                    unbox<string> inObj?sessionID

            let c =
                if isNull inObj?callID then
                    ""
                else
                    unbox<string> inObj?callID

            let argsStr =
                if isNull outObj || isNull outObj?args then
                    "{}"
                else
                    Fable.Core.JS.JSON.stringify outObj?args

            let outStr =
                if isNull outObj || isNull outObj?output then
                    "{}"
                else
                    Fable.Core.JS.JSON.stringify outObj?output

            if not (String.IsNullOrEmpty s) then
                let sr = rt.GetOrCreateSessionRuntime(SessionId.create s)
                sr.Inbox.TryPost(ToolAfterEvent(t, c, argsStr, outStr)) |> ignore

    let private handleCommand (rt: PluginRuntime) (inObj: obj) =
        if not (isNull inObj) then
            let cmdName = if isNull inObj?name then "" else unbox<string> inObj?name

            let s =
                if isNull inObj?sessionID then
                    ""
                else
                    unbox<string> inObj?sessionID

            let argsText =
                if isNull inObj?arguments then
                    ""
                else
                    unbox<string> inObj?arguments

            if not (String.IsNullOrEmpty s) then
                let sr = rt.GetOrCreateSessionRuntime(SessionId.create s)

                if cmdName = "loop" || cmdName = "/loop" then
                    sr.Inbox.TryPost(LoopCommandEvent(SessionId.create s, argsText)) |> ignore
                elif cmdName = "squad" || cmdName = "/squad" then
                    sr.Inbox.TryPost(SquadCommandEvent(s, argsText)) |> ignore

    let private handleConfig (config: obj) =
        if not (isNull config) then
            let commands =
                if isNull config?command then
                    createObj []
                else
                    config?command

            if isNull commands?loop then
                commands?loop <-
                    createObj
                        [ "template", box "$ARGUMENTS"
                          "description", box "Continue work until the task is complete." ]

            if isNull commands?squad then
                commands?squad <-
                    createObj
                        [ "template", box "$ARGUMENTS"
                          "description", box "Delegate work to a coordinated agent squad." ]

            config?command <- commands

    let private handleCompacting (rt: PluginRuntime) (outObj: obj) =
        if not (isNull outObj) then
            let proj = rt.Gateway.ProjectionSet

            let revInfo =
                match proj.LastReview with
                | Some v -> sprintf "Review: %A\n" v
                | None -> ""

            let todoInfo =
                match proj.Todos with
                | Some t -> sprintf "Todos: %s" (String.concat "; " t.Items)
                | None -> ""

            let ctxStr = (revInfo + todoInfo).Trim()

            if not (String.IsNullOrEmpty ctxStr) then
                outObj?context <- ctxStr

    let initPlugin (input: obj) : Task<obj> =
        task {
            let dir =
                if isNull input || isNull input?directory then
                    "."
                else
                    unbox<string> input?directory

            let portOpt = OpenCodePort.create input
            let! rtRes = PluginRuntime.start dir portOpt
            let rt =
                match rtRes with
                | Ok runtime -> runtime
                | Error err -> failwithf "Failed to initialize PluginRuntime: %A" err

            let toolsObj = PluginTools.buildToolsObject rt

            let hooks =
                {| ``chat.message`` =
                    fun (inObj: obj) (outObj: obj) ->
                        let hookInput = buildHookInput inObj
                        if not (String.IsNullOrEmpty hookInput.sessionID) then
                            rt.EnsureSessionDriver(SessionId.create hookInput.sessionID) |> ignore
                        OpencodeHooks.handleChatMessage rt.Gateway rt.SessionDrivers (rt.GetInboxMap()) hookInput outObj
                   ``chat.transform`` = fun (inObj: obj) (outObj: obj) -> handleChatTransform rt outObj
                   ``tool.definition`` = fun (inObj: obj) (outObj: obj) -> handleToolDefinition outObj
                   ``tool.execute.before`` =
                    fun (inObj: obj) (outObj: obj) ->
                        let toolIn: OpencodeToolExecuteInput =
                            { tool = unbox<string> inObj?tool
                              sessionID = unbox<string> inObj?sessionID
                              callID = unbox<string> inObj?callID }

                        let toolOut: OpencodeToolExecuteOutput = { args = outObj?args }
                        OpencodeHooks.handleToolExecuteBefore toolIn toolOut
                   ``tool.execute.after`` = fun (inObj: obj) (outObj: obj) -> handleToolExecuteAfter rt inObj outObj
                   config = fun (config: obj) -> handleConfig config
                   ``command.execute.before`` = fun (inObj: obj) (outObj: obj) -> handleCommand rt inObj
                   event =
                    fun (eventObj: obj) ->
                        OpencodeHooks.handleEvent rt.Gateway (rt.GetInboxMap()) eventObj
                   ``experimental.session.compacting`` = fun (inObj: obj) (outObj: obj) -> handleCompacting rt outObj
                   ``experimental.compaction.autocontinue`` =
                    fun (inObj: obj) (outObj: obj) ->
                        if not (isNull outObj) then
                            outObj?enabled <- true
                   command = fun (inObj: obj) (outObj: obj) -> handleCommand rt inObj
                   getOrCreateInbox = fun (sessionId: SessionId) -> (rt.GetOrCreateSessionRuntime sessionId).Inbox
                   tool = toolsObj
                   dispose =
                    fun () ->
                        task {
                            do! (rt :> IAsyncDisposable).DisposeAsync()
                        }
                |}

            return box hooks
        }

    [<ExportDefault>]
    let defaultExport =
        createObj
            [ "id", box "wanxiangshu-next"
              "server", box (fun (input: obj) -> initPlugin input) ]
