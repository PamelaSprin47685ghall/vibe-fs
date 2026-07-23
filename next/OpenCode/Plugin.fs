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

    let mutable private activeRuntime: PluginRuntime option = None

    let getOrCreateInbox (sessionId: SessionId) : ISessionInbox =
        match activeRuntime with
        | Some rt -> (rt.GetOrCreateSessionRuntime sessionId).Inbox
        | None -> FifoInbox(1000) :> ISessionInbox

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

    let private handleChatTransform (outObj: obj) =
        if not (isNull outObj) then
            match activeRuntime with
            | Some rt ->
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
            | None -> ()

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

    let private handleToolExecuteAfter (inObj: obj) (outObj: obj) =
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
                    sprintf "%A" outObj?args

            let outStr =
                if isNull outObj || isNull outObj?output then
                    "{}"
                else
                    sprintf "%A" outObj?output

            if not (String.IsNullOrEmpty s) then
                let ib = getOrCreateInbox (SessionId.create s)
                ib.TryPost(ToolAfterEvent(t, c, argsStr, outStr)) |> ignore

    let private handleCommand (inObj: obj) =
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
                let ib = getOrCreateInbox (SessionId.create s)

                if cmdName = "loop" || cmdName = "/loop" then
                    ib.TryPost(LoopCommandEvent(SessionId.create s, argsText)) |> ignore
                elif cmdName = "squad" || cmdName = "/squad" then
                    ib.TryPost(SquadCommandEvent(s, argsText)) |> ignore

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

    let private handleCompacting (outObj: obj) =
        if not (isNull outObj) then
            match activeRuntime with
            | Some rt ->
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
            | None -> ()

    let initPlugin (input: obj) : Task<obj> =
        task {
            let dir =
                if isNull input || isNull input?directory then
                    "."
                else
                    unbox<string> input?directory

            let port = OpenCodePort.HttpPort "http://127.0.0.1:4096" :> IOpenCodePort
            let! rtRes = PluginRuntime.start dir (Some port)
            match rtRes with
            | Ok rt -> activeRuntime <- Some rt
            | Error _ -> ()

            let hooks =
                {| ``chat.message`` =
                    fun (inObj: obj) (outObj: obj) ->
                        match activeRuntime with
                        | Some rt ->
                            let hookInput = buildHookInput inObj
                            rt.EnsureSessionDriver(SessionId.create hookInput.sessionID) |> ignore
                            OpencodeHooks.handleChatMessage rt.Gateway rt.SessionDrivers (rt.GetInboxMap()) hookInput outObj
                        | None -> ()
                   ``chat.transform`` = fun (inObj: obj) (outObj: obj) -> handleChatTransform outObj
                   ``tool.definition`` = fun (inObj: obj) (outObj: obj) -> handleToolDefinition outObj
                   ``tool.execute.before`` =
                    fun (inObj: obj) (outObj: obj) ->
                        let toolIn: OpencodeToolExecuteInput =
                            { tool = unbox<string> inObj?tool
                              sessionID = unbox<string> inObj?sessionID
                              callID = unbox<string> inObj?callID }

                        let toolOut: OpencodeToolExecuteOutput = { args = outObj?args }
                        OpencodeHooks.handleToolExecuteBefore toolIn toolOut
                   ``tool.execute.after`` = fun (inObj: obj) (outObj: obj) -> handleToolExecuteAfter inObj outObj
                   config = fun (config: obj) -> handleConfig config
                   ``command.execute.before`` = fun (inObj: obj) (outObj: obj) -> handleCommand inObj
                   event =
                    fun (eventObj: obj) ->
                        match activeRuntime with
                        | Some rt -> OpencodeHooks.handleEvent rt.Gateway (rt.GetInboxMap()) eventObj
                        | None -> ()
                   ``experimental.session.compacting`` = fun (inObj: obj) (outObj: obj) -> handleCompacting outObj
                   ``experimental.compaction.autocontinue`` =
                    fun (inObj: obj) (outObj: obj) ->
                        if not (isNull outObj) then
                            outObj?enabled <- true
                   command = fun (inObj: obj) (outObj: obj) -> handleCommand inObj
                   dispose =
                    fun () ->
                        match activeRuntime with
                        | Some rt -> (rt :> IAsyncDisposable).DisposeAsync() |> ignore
                        | None -> () |}

            return box hooks
        }

    [<ExportDefault>]
    let defaultExport =
        createObj
            [ "id", box "wanxiangshu-next"
              "server", box (fun (input: obj) -> initPlugin input) ]
