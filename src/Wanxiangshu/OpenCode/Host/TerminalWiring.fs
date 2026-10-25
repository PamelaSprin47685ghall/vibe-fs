namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.TerminalSignal

[<RequireQualifiedAccess>]
module TerminalWiring =

    [<Import("TerminalRegistry", "wanxiangli/terminal")>]
    let private terminalRegistryCtor: obj = jsNative

    [<Import("TerminalBus", "wanxiangli/terminal")>]
    let private terminalBusCtor: obj = jsNative

    [<Import("TerminalOutcome", "wanxiangli/terminal")>]
    let private terminalOutcomeCtor: obj = jsNative

    let private sharedTerminalBus: obj = createNew terminalBusCtor ()

    let private writeStderr (msg: string) : unit =
        let proc = emitJsExpr<obj> () "process"
        let stderr = proc?stderr
        if not (isNull stderr) then
            stderr?write(msg + "\n") |> ignore

    let private defaultReportFault (err: obj) : unit =
        let msg = if isNull err then "unknown error" else string err
        writeStderr ("terminal registry dispatch fault: " + msg)

    let private createRegistry (reportFault: (obj -> unit) option) : obj =
        let handler = defaultArg reportFault defaultReportFault
        createNew terminalRegistryCtor handler

    let private toNeutralOutcome (outcome: TerminalOutcome) : obj =
        match outcome with
        | TerminalOutcome.Completed result ->
            let runId = ProviderRunIdentity.value result.ProviderRun
            terminalOutcomeCtor?Completed(runId, box outcome)
        | TerminalOutcome.Failed _ ->
            terminalOutcomeCtor?Failed(box outcome)
        | TerminalOutcome.Aborted _ ->
            terminalOutcomeCtor?Aborted(box outcome)

    let private fromNeutralOutcome (neutral: obj) : TerminalOutcome =
        let p = neutral?Payload
        if isNull p then
            unbox<TerminalOutcome> neutral
        else
            unbox<TerminalOutcome> p

    let private wrapRegistry (reg: obj) : ISessionTerminalSource =
        { new ISessionTerminalSource with
            member _.SubscribeTerminal(sessionId, listener) =
                let sid = SessionId.value sessionId
                let rawUnsub = reg?SubscribeTerminal(sid, fun o -> listener sessionId (fromNeutralOutcome o))
                { new IDisposable with
                    member _.Dispose() = rawUnsub?dispose()
                }

            member _.SubscribeFutureTerminal(sessionId, listener) =
                let sid = SessionId.value sessionId
                let rawUnsub = reg?SubscribeFutureTerminal(sid, fun o -> listener sessionId (fromNeutralOutcome o))
                { new IDisposable with
                    member _.Dispose() = rawUnsub?dispose()
                }

            member _.SubscribeTerminalListener(listener) =
                let rawUnsub = reg?SubscribeProcessTerminal(fun sid o -> listener (SessionId.create sid) (fromNeutralOutcome o))
                { new IDisposable with
                    member _.Dispose() = rawUnsub?dispose()
                }

            member _.SubscribeFutureTerminalListener(listener) =
                let rawUnsub = reg?SubscribeProcessTerminal(fun sid o -> listener (SessionId.create sid) (fromNeutralOutcome o))
                { new IDisposable with
                    member _.Dispose() = rawUnsub?dispose()
                }

            member _.HasSubscriber(sessionId) =
                let sid = SessionId.value sessionId
                unbox<bool> (reg?HasSubscriber(sid))

            member _.NotifyTerminal sessionId outcome =
                let sid = SessionId.value sessionId
                let neutral = toNeutralOutcome outcome
                unbox<bool> (reg?NotifyTerminal(sid, neutral))
        }

    let createSource () : ISessionTerminalSource =
        wrapRegistry (createRegistry None)

    let acquire (directory: string) (candidate: ISessionTerminalSource) : ISessionTerminalSource =
        let acquired = sharedTerminalBus?Acquire(directory, candidate)
        unbox<ISessionTerminalSource> acquired

    let release (directory: string option) (source: ISessionTerminalSource option) : unit =
        match directory, source with
        | Some d, Some s ->
            sharedTerminalBus?Release(d, s) |> ignore
        | _ -> ()

    let tryAcquireForWorkspace
        (workspace: string option)
        (candidate: ISessionTerminalSource)
        : Task<(string * ISessionTerminalSource) option> =
        task {
            match workspace with
            | Some dir ->
                let acquired = acquire dir candidate
                return Some(dir, acquired)
            | None ->
                return None
        }
