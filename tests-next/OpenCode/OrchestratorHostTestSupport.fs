namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// Fake session port for OrchestratorHost tests. File name ends with
/// "TestSupport" so the test runner skips it during discovery.
module OrchestratorHostTestSupport =

    type CallLog =
        { mutable CreateChild: (string * string option * string option) list
          mutable SendPrompt: (string * string * string option * string option) list
          mutable FireAndForget: (string * string * string option * string option) list
          mutable Subscribed: string list }

    type FakeSessionPort(log: CallLog) =
        let terminalListeners = Dictionary<string, TerminalCompletionListener>()
        let nextChild = ref 0
        member _.Listeners = terminalListeners

        interface ISessionHostPort with
            member _.SubscribeTerminal(sessionId, listener) =
                let id = SessionId.value sessionId
                terminalListeners.[id] <- listener
                log.Subscribed <- id :: log.Subscribed

                { new IDisposable with
                    member _.Dispose() = terminalListeners.Remove(id) |> ignore }

            member _.SendPrompt(sessionId, text, opts) =
                let id = SessionId.value sessionId
                log.SendPrompt <- (id, text, opts.Agent, opts.Directory) :: log.SendPrompt
                Task.FromResult(Ok(MessageId.create "msg"))

            member _.SendChildPromptFireAndForget(parentId, childId, text, opts) =
                let id = SessionId.value childId
                log.FireAndForget <- (id, text, opts.Agent, opts.Directory) :: log.FireAndForget
                Task.FromResult(Ok())

            member _.AbortSession(sessionId) = Task.FromResult(Ok())
            member _.AbortChildren(parentId) = Task.FromResult(())

            member _.CreateChildSession(parentId, options) =
                nextChild.Value <- nextChild.Value + 1
                let id = sprintf "child-%d" nextChild.Value
                log.CreateChild <- (id, options.Agent, options.Directory) :: log.CreateChild
                Task.FromResult(Ok(SessionId.create id))

            member _.GetSessionOutput(sessionId) = []

    let createLog () =
        { CreateChild = []
          SendPrompt = []
          FireAndForget = []
          Subscribed = [] }

    let fireTerminal (port: FakeSessionPort) childId outcome =
        match port.Listeners.TryGetValue childId with
        | true, listener -> listener (SessionId.create childId) outcome
        | false, _ -> ()
