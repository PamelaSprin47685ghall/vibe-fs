namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.TerminalSignal

[<RequireQualifiedAccess>]
module TerminalWiring =

    let private writeStderr (msg: string) =
        let proc = emitJsExpr<obj> () "process"
        let stderr = proc?stderr
        if not (isNull (box stderr)) then
            stderr?write (msg + "\n") |> ignore

    let private defaultReportFault (ex: exn) =
        let msg = sprintf "[TerminalWiring] Listener fault: %s" ex.Message
        writeStderr msg

    let fromNeutralOutcome (neutral: obj) : TerminalOutcome =
        unbox<TerminalOutcome> neutral

    let createRegistry (reportFault: (exn -> unit) option) : obj =
        let report = defaultArg reportFault defaultReportFault
        let jsReport = Func<obj, unit>(fun err ->
            match err with
            | :? exn as e -> report e
            | other -> report (Exception(string other)))
        let ctor = emitJsExpr<obj> () "require('wanxiangli/terminal').TerminalRegistry"
        createNew ctor (jsReport)

    let private globalBusInstance: obj =
        let ctor = emitJsExpr<obj> () "require('wanxiangli/terminal').TerminalBus"
        createNew ctor ()

    let wrapRegistry (registry: obj) : ISessionTerminalSource =
        { new ISessionTerminalSource with
            member _.SubscribeTerminal(sessionId, listener) =
                let jsListener = Action<obj>(fun o -> listener sessionId (fromNeutralOutcome o))
                let sub = registry?SubscribeTerminal(string sessionId, jsListener)
                { new IDisposable with
                    member _.Dispose() = sub?Dispose() }

            member _.SubscribeFutureTerminal(sessionId, listener) =
                let jsListener = Action<obj>(fun o -> listener sessionId (fromNeutralOutcome o))
                let sub = registry?SubscribeFutureTerminal(string sessionId, jsListener)
                { new IDisposable with
                    member _.Dispose() = sub?Dispose() }

            member _.SubscribeTerminalListener(listener) =
                let jsListener = Action<obj>(fun o -> listener (SessionId.create "global") (fromNeutralOutcome o))
                let sub = registry?SubscribeProcessTerminal(jsListener)
                { new IDisposable with
                    member _.Dispose() = sub?Dispose() }

            member _.SubscribeFutureTerminalListener(listener) =
                let jsListener = Action<obj>(fun o -> listener (SessionId.create "global") (fromNeutralOutcome o))
                let sub = registry?SubscribeProcessTerminal(jsListener)
                { new IDisposable with
                    member _.Dispose() = sub?Dispose() }

            member _.HasSubscriber(sessionId) =
                registry?HasSubscriber(string sessionId) |> unbox<bool>

            member _.NotifyTerminal sessionId outcome =
                registry?NotifyTerminal(string sessionId, box outcome) |> unbox<bool> }

    let createSource () : ISessionTerminalSource =
        let reg = createRegistry None
        wrapRegistry reg

    let acquire (directory: string) (candidate: ISessionTerminalSource) : ISessionTerminalSource =
        let shared = globalBusInstance?Acquire(directory, box candidate)
        unbox<ISessionTerminalSource> shared

    let release (directory: string option) (source: ISessionTerminalSource option) : unit =
        match directory, source with
        | Some dir, Some src ->
            globalBusInstance?Release(dir, box src) |> ignore
        | _ -> ()

    let tryAcquireForWorkspace
        (workspace: string option)
        (candidate: ISessionTerminalSource)
        : Task<(string * ISessionTerminalSource) option> =
        match workspace with
        | Some dir when not (String.IsNullOrWhiteSpace dir) ->
            let src = acquire dir candidate
            Task.FromResult(Some(dir, src))
        | _ ->
            Task.FromResult None
