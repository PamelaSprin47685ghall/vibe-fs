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

    let private sessionDrivers = SessionDrivers()
    let private inboxes = Dictionary<SessionId, ISessionInbox>()
    let mutable private activeGateway: Gateway option = None

    let getOrCreateInbox (sessionId: SessionId) : ISessionInbox =
        lock inboxes (fun () ->
            match inboxes.TryGetValue(sessionId) with
            | true, inbox -> inbox
            | false, _ ->
                let inbox = FifoInbox(1000) :> ISessionInbox
                inboxes.[sessionId] <- inbox
                inbox)

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
            match activeGateway with
            | Some gw ->
                let proj = gw.ProjectionSet
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

    let private handleToolDefinition (outObj: obj) = ()

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

    let private handleCompacting (outObj: obj) =
        if not (isNull outObj) then
            match activeGateway with
            | Some gw ->
                let proj = gw.ProjectionSet

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

            let cts = new CancellationTokenSource()
            let! gwResult = Gateway.start dir cts.Token

            match gwResult with
            | Ok gw -> activeGateway <- Some gw
            | Error _ -> ()

            let hooks =
                {| ``chat.message`` =
                    fun (inObj: obj) (outObj: obj) ->
                        match activeGateway with
                        | Some gw ->
                            OpencodeHooks.handleChatMessage gw sessionDrivers inboxes (buildHookInput inObj) outObj
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
                   event =
                    fun (eventObj: obj) ->
                        match activeGateway with
                        | Some gw -> OpencodeHooks.handleEvent gw inboxes eventObj
                        | None -> ()
                   ``experimental.session.compacting`` = fun (inObj: obj) (outObj: obj) -> handleCompacting outObj
                   ``experimental.compaction.autocontinue`` =
                    fun (inObj: obj) (outObj: obj) ->
                        if not (isNull outObj) then
                            outObj?enabled <- true
                   command = fun (inObj: obj) (outObj: obj) -> handleCommand inObj
                   dispose =
                    fun () ->
                        match activeGateway with
                        | Some gw -> (gw :> IAsyncDisposable).DisposeAsync() |> ignore
                        | None -> () |}

            return box hooks
        }
