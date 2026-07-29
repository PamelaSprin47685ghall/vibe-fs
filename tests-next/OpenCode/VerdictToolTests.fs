namespace Wanxiangshu.Next.Tests.OpenCodeTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module VerdictToolTests =

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
                Task.FromResult(Ok(SessionId.create "child"))

            member _.GetSessionOutput(_) = [] }

    [<Emit("(() => { const node = {}; node.optional = () => node; node.describe = () => node; const schema = { string: () => node, number: () => node, enum: () => node, union: () => node, array: () => node }; const factory = definition => definition; factory.schema = schema; return { tool: factory }; })()")>]
    let private fakeToolModule () : obj = jsNative

    [<Emit("$0[$1]")>]
    let private toolNamed (tools: obj) (name: string) : obj = jsNative

    [<Emit("$0.execute($1, $2)")>]
    let private executeTool (tool: obj) (args: obj) (context: obj) : Task<obj> = jsNative

    [<Fact>]
    let ``Verdict tool returns one skeptical sentence for first PERFECT`` () =
        withTempDir (fun directory ->
            task {
                let managerId = "review-manager-surface"
                let reviewerId = "reviewer-surface"
                let now = DateTimeOffset.UtcNow

                use journal =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "review-runtime-surface")
                        1
                        now
                        (Boot.boot directory)

                let parents = Dictionary<string, string>()
                parents.[reviewerId] <- managerId

                let registration =
                    ToolRegistry.create
                        (fakeToolModule ())
                        (hostPort ())
                        (Some journal)
                        (Some { GetTreeHash = fun () -> "tree-surface" })
                        None
                        parents
                        (Dictionary<string, string>())
                        (fun _ -> None)
                        (HashSet<string>())
                        (Dictionary<string, string>())
                        None
                        None
                        None
                        None
                        None

                let verdictTool = toolNamed registration.Tools "verdict"
                let execute args context = executeTool verdictTool args context
                let args = createObj [ "verdict", box "PERFECT" ]

                let context =
                    createObj
                        [ "sessionID", box reviewerId
                          "agent", box "fast-reviewer"
                          "toolCallId", box "call-surface-1"
                          "messageID", box "provider-surface-1"
                          "prompt", box "Review the current worktree for correctness." ]

                let! result = execute args context

                Assert.Equal(
                    "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?",
                    unbox<string> result
                )

                let reviseArgs = createObj [ "verdict", box "REVISE" ]

                let reviseContext =
                    createObj
                        [ "sessionID", box reviewerId
                          "agent", box "fast-reviewer"
                          "toolCallId", box "call-surface-2"
                          "messageID", box "provider-surface-2"
                          "prompt", box "Review the current worktree for correctness." ]

                let! reviseResult = execute reviseArgs reviseContext
                Assert.Equal("REVISE recorded for the current tree.", unbox<string> reviseResult)

                let invalidArgs = createObj [ "verdict", box "MAYBE" ]
                let! invalidResult = execute invalidArgs context
                Assert.True((unbox<string> invalidResult).StartsWith("Verdict rejected:"))
                registration.Runtime.Dispose()
            })
