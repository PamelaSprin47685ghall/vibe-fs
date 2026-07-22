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
                            let hookInput: OpencodeHookInput =
                                { sessionID = unbox<string> inObj?sessionID
                                  messageID =
                                    if isNull inObj?messageID then
                                        None
                                    else
                                        Some(unbox<string> inObj?messageID)
                                  agent =
                                    if isNull inObj?agent then
                                        None
                                    else
                                        Some(unbox<string> inObj?agent)
                                  model = None }

                            OpencodeHooks.handleChatMessage gw sessionDrivers hookInput outObj
                        | None -> ()
                   ``tool.execute.before`` =
                    fun (inObj: obj) (outObj: obj) ->
                        let toolIn: OpencodeToolExecuteInput =
                            { tool = unbox<string> inObj?tool
                              sessionID = unbox<string> inObj?sessionID
                              callID = unbox<string> inObj?callID }

                        let toolOut: OpencodeToolExecuteOutput = { args = outObj?args }
                        OpencodeHooks.handleToolExecuteBefore toolIn toolOut
                   event =
                    fun (eventObj: obj) ->
                        match activeGateway with
                        | Some gw -> OpencodeHooks.handleEvent gw inboxes eventObj
                        | None -> () |}

            return box hooks
        }
