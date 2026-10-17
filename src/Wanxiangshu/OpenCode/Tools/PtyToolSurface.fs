namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

[<RequireQualifiedAccess>]
module PtyToolSurface =
    [<Emit("$0.execute($1, $2)")>]
    let private invoke (tool: obj) (args: obj) (context: obj) : Task<string> = jsNative

    let nameRace () : Task<obj> =
        task {
            let exits = Dictionary<PtyId, TaskCompletionSource<unit>>()
            let terminated = TaskCompletionSource<PtyId>()
            // DSL-MUTABLE: resource — controlled backend exit-observation port
            let mutable portRef: PtyPort option = None

            let handler id command =
                let port =
                    portRef
                    |> Option.defaultWith (fun () -> invalidOp "PTY fixture is not initialized")

                match command with
                | PtyCommand.Spawn _ ->
                    let waiting = TaskCompletionSource<unit>()
                    exits.[id] <- waiting
                    port.RegisterExitTask(id, waiting.Task)
                | PtyCommand.Signal _ -> terminated.SetResult id
                | _ -> ()

                Task.FromResult(Ok())

            let port = PtyPort(handler = handler)
            portRef <- Some port

            let runtime =
                HostForkRuntime(
                    SessionId.create "terminal-name-race",
                    Unchecked.defaultof<ISessionHostPort>,
                    (fun _ _ _ -> Task.FromResult None),
                    CompletionMailboxRuntime.create,
                    NodeTiming.nodeClockPort (),
                    NodeTiming.raceExit,
                    ptyPort = port
                )

            let capability = runtime.PtyCapability
            // DSL-MUTABLE: algorithm-scratch — count concurrent fixture openings
            let mutable openings = 0
            let openedBoth = TaskCompletionSource<unit>()

            let fork args =
                task {
                    let! opened = capability.ForkPty args
                    openings <- openings + 1

                    if openings = 2 then
                        openedBoth.SetResult(())

                    do! openedBoth.Task
                    return opened
                }

            let context: PtyTool.PtyRuntimeContext =
                { IsDevOps = fun _ -> true
                  ManagedAgentFor = fun _ -> Some(ManagedAgent.make Role.DevOps)
                  PtyCapabilityFor = fun _ -> Ok { capability with ForkPty = fork }
                  DirectoryFor = fun _ -> None
                  WorkspaceDirectory = None }

            let toolModule: obj =
                emitJsExpr () "({ tool: Object.assign(spec => spec, { schema: { string: () => ({}) } }) })"

            let factory = ToolHostCodec.factory toolModule
            let registered = PtyTool.openSpec factory context |> ToolHostCodec.register factory
            let args = createObj [ "name" ==> "same"; "command" ==> "long-running" ]
            let host = createObj [ "sessionID" ==> "terminal-name-race" ]
            // DSL-MUTABLE: algorithm-scratch — count concurrent fixture replies
            let mutable returned = 0

            let openOnce () =
                task {
                    let! result = invoke registered args host
                    returned <- returned + 1
                    return result
                }

            let first = openOnce ()
            let second = openOnce ()
            let! loser = terminated.Task
            let loserLiveBeforeExit = port.Exists loser
            let loserOwnedBeforeExit = runtime.OwnsPty loser
            let outstandingBeforeExit = runtime.SnapshotOutstandingPtyRuns().Length
            let bothReturnedBeforeExit = returned = 2
            port.Complete loser
            exits.[loser].SetResult(())
            let! firstResult = first
            let! secondResult = second
            let results = [| firstResult; secondResult |]
            let remaining = runtime.SnapshotOutstandingPtyRuns()

            for id in remaining do
                let key = PtyId.Create id
                port.Complete key
                exits.[key].SetResult(())

            return
                box
                    {| loserLiveBeforeExit = loserLiveBeforeExit
                       loserOwnedBeforeExit = loserOwnedBeforeExit
                       outstandingBeforeExit = outstandingBeforeExit
                       bothReturnedBeforeExit = bothReturnedBeforeExit
                       bindingErrors =
                        results
                        |> Array.filter (fun result -> result.Contains "already in use")
                        |> Array.length
                       outstandingAfterExit = remaining.Length |}
        }
