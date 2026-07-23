namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

type SessionRuntime =
    { SessionId: SessionId
      Inbox: ISessionInbox
      Driver: SessionDriver option
      Cts: CancellationTokenSource }

type PluginRuntime private (gateway: Gateway, dir: string, port: IOpenCodePort option) =
    let sessionRuntimes = Dictionary<SessionId, SessionRuntime>()
    let sessionDrivers = SessionDrivers()
    let lockObj = obj ()
    let cts = new CancellationTokenSource()

    member _.Gateway = gateway
    member _.Directory = dir
    member _.CancellationToken = cts.Token
    member _.SessionDrivers = sessionDrivers
    member _.Port = port

    static member start (dir: string) (port: IOpenCodePort option) : Task<Result<PluginRuntime, GatewayError>> =
        task {
            let cts = new CancellationTokenSource()
            let! gwRes = Gateway.start dir cts.Token
            match gwRes with
            | Ok gw -> return Ok(new PluginRuntime(gw, dir, port))
            | Error err -> return Error err
        }

    member this.GetOrCreateSessionRuntime(sessionId: SessionId) : SessionRuntime =
        lock lockObj (fun () ->
            match sessionRuntimes.TryGetValue(sessionId) with
            | true, sr -> sr
            | false, _ ->
                let inbox = FifoInbox(1000) :> ISessionInbox
                let sessionCts = new CancellationTokenSource()
                let sr = { SessionId = sessionId; Inbox = inbox; Driver = None; Cts = sessionCts }
                sessionRuntimes.[sessionId] <- sr
                sr)

    member this.EnsureSessionDriver(sessionId: SessionId) : SessionRuntime =
        lock lockObj (fun () ->
            let sessionRuntime = this.GetOrCreateSessionRuntime(sessionId)

            match sessionRuntime.Driver with
            | Some _ -> sessionRuntime
            | None ->
                let promptPort = port |> Option.map (fun value -> value :> IPromptPort)
                let driver = new SessionDriver(gateway, sessionId, sessionRuntime.Inbox, ?port = promptPort)
                let runningRuntime = { sessionRuntime with Driver = Some driver }
                sessionRuntimes.[sessionId] <- runningRuntime
                runningRuntime)

    member this.GetInboxMap() : Dictionary<SessionId, ISessionInbox> =
        lock lockObj (fun () ->
            let dict = Dictionary<SessionId, ISessionInbox>()
            for kv in sessionRuntimes do
                dict.[kv.Key] <- kv.Value.Inbox
            dict)

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            task {
                let runtimesToDispose =
                    lock lockObj (fun () ->
                        let list = List<SessionRuntime>(sessionRuntimes.Values)
                        sessionRuntimes.Clear()
                        list)

                cts.Cancel()

                for sr in runtimesToDispose do
                    sr.Cts.Cancel()
                    match sr.Driver with
                    | Some d ->
                        (d :> IDisposable).Dispose()
                        try
                            do! d.Worker
                        with _ ->
                            ()
                    | None -> ()
                    sr.Cts.Dispose()

                cts.Dispose()
                let! _ = (gateway :> IAsyncDisposable).DisposeAsync()
                return ()
            }
            |> unbox<ValueTask>
