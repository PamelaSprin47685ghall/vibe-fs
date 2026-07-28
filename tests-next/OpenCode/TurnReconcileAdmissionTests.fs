namespace Wanxiangshu.Next.Tests.OpenCode

open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session

module TurnReconcileAdmissionTests =

    let private textPart text =
        createObj [ "id", box "p1"; "type", box "text"; "text", box text ]

    let private msg id role agent finish parts =
        { Id = MessageId.create id
          Role = role
          Agent = agent
          Finish = finish
          ErrorName = None
          Model = None
          Parts = parts
          Raw = createObj [] }

    let private binding root physical continuations role =
        { SessionId = SessionId.create "s-admission"
          RunId = None
          RootUserMessageId = Some(MessageId.create root)
          PhysicalUserMessageId = Some(MessageId.create physical)
          ContinuationMessageIds = continuations
          AgentRole = Some role
          Directory = "/tmp/ws" }

    [<Fact>]
    let ``Admission_root_resolves_first_physical_user`` () =
        let messages =
            [ msg "u-real" "user" None None [||]
              msg "a-real" "assistant" (Some "manager") (Some "stop") [| textPart "done" |] ]

        let turn =
            TurnReconcile.reconcile
                messages
                (binding "accepted-s-admission" "accepted-s-admission" Set.empty AgentRole.Manager)
            |> Option.get

        Assert.Equal("u-real", MessageId.value turn.RootUserMessageId)
        Assert.Equal("u-real", MessageId.value turn.UserMessageId)

    [<Fact>]
    let ``Admission_continuation_resolves_newest_physical_user`` () =
        let admission = "accepted-s-admission"

        let messages =
            [ msg "u-root" "user" None None [||]
              msg "a-first" "assistant" (Some "reviewer") (Some "tool-calls") [||]
              msg "u-confirm" "user" None None [||]
              msg "a-confirm" "assistant" (Some "reviewer") (Some "stop") [| textPart "confirmed" |] ]

        let turn =
            TurnReconcile.reconcile messages (binding admission admission (Set.singleton admission) AgentRole.Reviewer)
            |> Option.get

        Assert.Equal("u-root", MessageId.value turn.RootUserMessageId)
        Assert.Equal("u-confirm", MessageId.value turn.UserMessageId)
