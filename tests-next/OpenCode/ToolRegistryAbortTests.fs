namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.OpenCode

module ToolRegistryAbortTests =

    let private hostPort () =
        { new ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                Task.FromResult(Ok(MessageId.create "accepted"))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())

            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task

            member _.CreateChildSession(_, _) =
                Task.FromResult(Ok(SessionId.create "child-abort"))

            member _.GetSessionOutput(_) = [] }

    let private restartableHostPort () =
        let mutable childNumber = 0

        { new ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                Task.FromResult(Ok(MessageId.create "accepted"))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())

            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task

            member _.CreateChildSession(_, _) =
                childNumber <- childNumber + 1
                Task.FromResult(Ok(SessionId.create (sprintf "child-after-esc-%d" childNumber)))

            member _.GetSessionOutput(_) = [] }

    [<Emit("(() => { const node = {}; node.optional = () => node; node.describe = () => node; const schema = { string: () => node, number: () => node, enum: () => node, union: () => node, array: () => node }; const factory = definition => definition; factory.schema = schema; return { tool: factory }; })()")>]
    let private fakeToolModule () : obj = jsNative

    [<Emit("$0[$1]")>]
    let private toolNamed (tools: obj) (name: string) : obj = jsNative

    [<Emit("$0.execute($1, $2)")>]
    let private executeTool (tool: obj) (args: obj) (context: obj) : Task<obj> = jsNative

    [<Fact>]
    let ``join_execute_with_aborted_signal_cancels_runtime_and_returns_CANCELLED`` () =
        task {
            let parentId = SessionId.create "parent-join-abort"
            let sessionId = SessionId.value parentId
            let sessionParents = Dictionary<string, string>()
            let sessionRoles = Dictionary<string, string>()
            sessionRoles.["parent-join-abort"] <- "manager"
            let sessionDirectories = Dictionary<string, string>()
            let signalCaptured = ResizeArray<SessionId>()
            let callbacks = ResizeArray<unit -> unit>()

            let registration =
                ToolRegistry.create
                    (fakeToolModule ())
                    (hostPort ())
                    None
                    None
                    None
                    sessionParents
                    sessionRoles
                    (fun _ -> None)
                    (HashSet<string>())
                    sessionDirectories
                    None
                    None
                    None
                    (Some(fun ids -> signalCaptured.AddRange ids))
                    None

            let join = toolNamed registration.Tools "join"

            let signal =
                createObj
                    [ "aborted", box true
                      "addEventListener",
                      box (fun (_evt: string) (cb: obj) (_opts: obj) -> callbacks.Add(unbox<unit -> unit> cb))
                      "removeEventListener", box (fun _ _ -> ()) ]

            let ctx = createObj [ "sessionID", box sessionId; "abort", box signal ]

            let! result = executeTool join (createObj []) ctx
            let output = result.ToString()

            Assert.True(output.Contains("CANCELLED"), output)
            Assert.True(signalCaptured.Contains(parentId), "signal cancel was not invoked for parent")
        }

    [<Fact>]
    let ``fork_execute_replaces_cancelled_runtime_after_Esc`` () =
        task {
            let sessionId = "parent-fork-after-esc"
            let sessionParents = Dictionary<string, string>()
            let sessionRoles = Dictionary<string, string>()
            sessionRoles.[sessionId] <- "manager"
            let sessionDirectories = Dictionary<string, string>()

            let registration =
                ToolRegistry.create
                    (fakeToolModule ())
                    (restartableHostPort ())
                    None
                    None
                    None
                    sessionParents
                    sessionRoles
                    (fun _ -> None)
                    (HashSet<string>())
                    sessionDirectories
                    None
                    None
                    None
                    None
                    None

            let fork = toolNamed registration.Tools "fork"
            let join = toolNamed registration.Tools "join"
            let normalContext = createObj [ "sessionID", box sessionId ]
            let forkArgs = createObj [ "agent", box "fast-coder"; "prompt", box "implement" ]

            let! initialFork = executeTool fork forkArgs normalContext
            Assert.True(initialFork.ToString().Contains("fast-coder"), initialFork.ToString())

            let abortSignal =
                createObj
                    [ "aborted", box true
                      "addEventListener", box (fun (_: string) (_: obj) (_: obj) -> ())
                      "removeEventListener", box (fun (_: obj) (_: obj) -> ()) ]

            let interruptedContext =
                createObj [ "sessionID", box sessionId; "abort", box abortSignal ]

            let! cancelledJoin = executeTool join (createObj []) interruptedContext
            Assert.True(cancelledJoin.ToString().Contains("CANCELLED"), cancelledJoin.ToString())

            let! forkAfterEsc = executeTool fork forkArgs normalContext
            Assert.True(forkAfterEsc.ToString().Contains("fast-coder"), forkAfterEsc.ToString())
        }
