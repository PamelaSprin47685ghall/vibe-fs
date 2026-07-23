namespace Wanxiangshu.Next.Tests.E2E

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module VerticalSliceE2ETests =

    [<Fact>]
    let Opencode_plugin_gateway_e2e_flow () =
        withTempDir (fun tempDir ->
            task {
                let initArg = createObj [ "directory", box tempDir ]
                let! hooksObj = Plugin.initPlugin initArg
                Assert.False(isNull hooksObj)

                let sessionId = SessionId.create "sess-e2e-flow"
                let inbox = Plugin.getOrCreateInbox sessionId

                let eventFn = unbox<obj -> unit> hooksObj?event
                let cmdFn = unbox<obj -> unit> hooksObj?command

                let cmdArg = createObj [ "name", box "loop"; "sessionID", box "sess-e2e-flow"; "arguments", box "task loop" ]
                cmdFn cmdArg

                let! ev1 = inbox.Receive CancellationToken.None

                match ev1 with
                | LoopCommandEvent(sId, text) ->
                    Assert.Equal("sess-e2e-flow", SessionId.value sId)
                    Assert.Equal("task loop", text)
                | other -> Assert.True(false, sprintf "Expected LoopCommandEvent, got %A" other)

                let evIdle = createObj [ "type", box "session.idle"; "properties", box (createObj [ "sessionID", box "sess-e2e-flow" ]) ]
                eventFn evIdle

                let! ev2 = inbox.Receive CancellationToken.None

                match ev2 with
                | LifecycleEvent kind -> Assert.Equal("session.idle", kind)
                | other -> Assert.True(false, sprintf "Expected session.idle, got %A" other)

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })
