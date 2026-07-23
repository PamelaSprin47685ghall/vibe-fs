namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module OpencodePluginTests =

    [<Emit("$0($1, $2)")>]
    let private call2 (fn: obj) (a: obj) (b: obj) : unit = jsNative

    [<Emit("$0($1)")>]
    let private call1 (fn: obj) (a: obj) : unit = jsNative


    [<Fact>]
    let ``Opencode_plugin_experimental_chat_messages_transform_hook`` () =
        withTempDir (fun tempDir ->
            task {
                let initArg = createObj [ "directory", box tempDir ]
                let! hooksObj = Plugin.initPlugin initArg
                let transformFn = hooksObj?``experimental.chat.messages.transform``

                let origMsg =
                    createObj [ "role", box "user"; "text", box "hello world"; "id", box "usr_msg_123" ]

                let outObj = createObj [ "messages", box [| origMsg |] ]

                call2 transformFn null outObj

                let msgs = unbox<obj list> outObj?messages
                Assert.True(List.length msgs > 1)
                let lastMsg = List.last msgs
                Assert.Equal("user", unbox<string> lastMsg?role)
                Assert.Equal("hello world", unbox<string> lastMsg?text)
                Assert.Equal("usr_msg_123", unbox<string> lastMsg?id)

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })

    let ``Opencode_plugin_initPlugin_returns_hooks_record`` () =
        withTempDir (fun tempDir ->
            task {
                let initArg = createObj [ "directory", box tempDir ]
                let! hooksObj = Plugin.initPlugin initArg
                Assert.False(isNull hooksObj)

                let hasProp (name: string) = not (isNull (hooksObj?(name)))

                Assert.True(hasProp "chat.message", "chat.message hook missing")
                Assert.True(hasProp "chat.transform", "chat.transform hook missing")

                Assert.True(
                    hasProp "experimental.chat.messages.transform",
                    "experimental.chat.messages.transform hook missing"
                )

                Assert.True(hasProp "tool.execute.before", "tool.execute.before hook missing")
                Assert.True(hasProp "tool.execute.after", "tool.execute.after hook missing")
                Assert.True(hasProp "config", "config hook missing")
                Assert.True(hasProp "command.execute.before", "command.execute.before hook missing")
                Assert.True(hasProp "command", "command hook missing")
                Assert.True(hasProp "event", "event hook missing")
                Assert.True(hasProp "experimental.session.compacting", "compacting hook missing")
                Assert.True(hasProp "experimental.compaction.autocontinue", "autocontinue hook missing")
                Assert.True(hasProp "dispose", "dispose hook missing")

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })

    [<Fact>]
    let ``Opencode_plugin_command_hook_handles_loop`` () =
        withTempDir (fun tempDir ->
            task {
                let initArg = createObj [ "directory", box tempDir ]
                let! hooksObj = Plugin.initPlugin initArg
                let sessionId = SessionId.create "sess-cmd-test"
                let getInbox = unbox<SessionId -> ISessionInbox> hooksObj?getOrCreateInbox
                let inbox = getInbox sessionId

                let cmdFn = unbox<obj -> unit> hooksObj?command

                let cmd1 =
                    createObj
                        [ "name", box "loop"
                          "sessionID", box "sess-cmd-test"
                          "arguments", box "do task X" ]

                cmdFn cmd1

                let! ev1 = inbox.Receive CancellationToken.None

                match ev1 with
                | LoopCommandEvent(sId, text) ->
                    Assert.Equal("sess-cmd-test", SessionId.value sId)
                    Assert.Equal("do task X", text)
                | other -> Assert.True(false, sprintf "Expected LoopCommandEvent, got %A" other)


                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })

    [<Fact>]
    let ``Opencode_plugin_config_hook_registers_loop`` () =
        withTempDir (fun tempDir ->
            task {
                let! hooksObj = Plugin.initPlugin (createObj [ "directory", box tempDir ])
                let configObj = createObj []
                call1 hooksObj?config configObj

                Assert.False(isNull configObj?command?loop)
                Assert.Equal("$ARGUMENTS", unbox<string> configObj?command?loop?template)

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })

    [<Fact>]
    let ``Opencode_plugin_tool_execute_before_clears_warning_fields`` () =
        withTempDir (fun tempDir ->
            task {
                let initArg = createObj [ "directory", box tempDir ]
                let! hooksObj = Plugin.initPlugin initArg

                let beforeFn = hooksObj?``tool.execute.before``

                let inObj =
                    createObj [ "tool", box "write"; "sessionID", box "s1"; "callID", box "c1" ]

                let argsObj =
                    createObj
                        [ "warn_tdd", box "yes"
                          "warn_reuse", box "yes"
                          "warn_context", box "yes"
                          "filePath", box "a.txt" ]

                let outObj = createObj [ "args", box argsObj ]

                call2 beforeFn inObj outObj

                Assert.True(isNull argsObj?warn_tdd)
                Assert.True(isNull argsObj?warn_reuse)
                Assert.True(isNull argsObj?warn_context)
                Assert.Equal("a.txt", unbox<string> argsObj?filePath)

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })

    [<Fact>]
    let ``Opencode_plugin_event_hook_forwards_lifecycle_and_terminal_events`` () =
        withTempDir (fun tempDir ->
            task {
                let initArg = {| directory = tempDir |}
                let! hooksObj = Plugin.initPlugin initArg
                let sessionId = SessionId.create "sess-ev-test"
                let getInbox = unbox<SessionId -> ISessionInbox> hooksObj?getOrCreateInbox
                let inbox = getInbox sessionId

                let eventFn = unbox<obj -> unit> hooksObj?event

                let evIdle =
                    createObj
                        [ "type", box "session.idle"
                          "properties", box (createObj [ "sessionID", box "sess-ev-test" ]) ]

                eventFn evIdle

                let! ev1 = inbox.Receive CancellationToken.None

                match ev1 with
                | LifecycleEvent kind -> Assert.Equal("session.idle", kind)
                | other -> Assert.True(false, sprintf "Expected session.idle, got %A" other)

                let msgInfo =
                    createObj
                        [ "role", box "assistant"
                          "id", box "msg_ast_1"
                          "parentID", box "msg_usr_1"
                          "error", null ]

                let evMsg =
                    createObj
                        [ "type", box "message.updated"
                          "properties", box (createObj [ "sessionID", box "sess-ev-test"; "info", box msgInfo ]) ]

                eventFn evMsg

                let! ev2 = inbox.Receive CancellationToken.None

                match ev2 with
                | AssistantTerminalEvent(userMsgId, astMsgId, outcome) ->
                    Assert.Equal("msg_usr_1", MessageId.value userMsgId)
                    Assert.Equal("msg_ast_1", MessageId.value astMsgId)
                    Assert.Equal(PromptOutcome.Delivered(MessageId.create "msg_ast_1"), outcome)
                | other -> Assert.True(false, sprintf "Expected AssistantTerminalEvent, got %A" other)

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })

    [<Fact>]
    let ``Opencode_plugin_compaction_and_autocontinue_hooks`` () =
        withTempDir (fun tempDir ->
            task {
                let initArg = createObj [ "directory", box tempDir ]
                let! hooksObj = Plugin.initPlugin initArg

                let autoFn = hooksObj?``experimental.compaction.autocontinue``
                let outAuto = createObj [ "enabled", box false ]
                call2 autoFn null outAuto
                Assert.True(unbox<bool> outAuto?enabled)

                let compFn = hooksObj?``experimental.session.compacting``
                let outComp = createObj [ "context", box "" ]
                call2 compFn null outComp

                let disposeFn = unbox<unit -> unit> hooksObj?dispose
                disposeFn ()
            })
