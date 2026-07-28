namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode

module PromptAuthorityTests =

    type private CapturingPort() =
        let mutable options: OpenCodePromptOptions option = None

        member _.Options = options

        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, opts) =
                options <- Some opts
                Task.FromResult(Ok(MessageId.create "continuation-1"))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(())
            member _.CreateChildSession(_, _) = Task.FromResult(Error "not used")
            member _.GetSessionOutput(_) = []

    [<Fact>]
    let ``Continuation acceptance preserves last authority profile`` () =
        let session = SessionId.create "s1"
        let root = MessageId.create "human-root"

        let profile =
            PromptAuthority.createAuthorityRoot session PromptAuthority.HumanRoot root "manager" None None

        let before = PromptAuthority.registerAuthority profile PromptAuthority.empty
        let key = PromptAuthority.newPromptKey ()

        let claim =
            PromptAuthority.claimContinuation key session PromptAuthority.InteractionRepair profile None

        let after =
            before
            |> PromptAuthority.registerClaim claim
            |> PromptAuthority.acceptClaim key (MessageId.create "repair-message")

        Assert.Equal(Some profile, after.LastAuthorityProfile)
        Assert.Equal(Some profile, after.ActiveLogicalRun)

        Assert.Equal(
            Some PromptAuthority.InteractionRepair,
            Map.tryFind (MessageId.create "repair-message") after.AcceptedContinuationIds
        )

    [<Fact>]
    let ``Dispatcher claims continuation and preserves authority metadata`` () =
        task {
            let session = SessionId.create "s1"
            let root = MessageId.create "human-root"

            let profile =
                PromptAuthority.createAuthorityRoot session PromptAuthority.HumanRoot root "manager" None None

            let port = CapturingPort()
            let dispatcher = PromptDispatcher.Dispatcher()
            dispatcher.RegisterAuthority profile

            let! accepted =
                dispatcher.SendContinuation
                    (port :> ISessionHostPort)
                    session
                    "\u200B"
                    PromptAuthority.InteractionRepair
                    profile
                    None
                    None
                    None

            Assert.Equal(Ok(MessageId.create "continuation-1"), accepted)
            Assert.Equal(Some profile, dispatcher.Projection.LastAuthorityProfile)

            match port.Options with
            | Some { Metadata = Some metadata } ->
                Assert.NotNull(metadata?wanxiangshu_prompt_key)
                Assert.Equal("InteractionRepair", unbox<string> metadata?wanxiangshu_origin)
                Assert.Equal("human-root", unbox<string> metadata?wanxiangshu_authority_root)
            | _ -> Assert.True(false, "dispatcher omitted continuation metadata")
        }

    [<Fact>]
    let ``Unknown physical user message never becomes authority`` () =
        let origin =
            PromptAuthority.resolveKnownOrigin (MessageId.create "unproven-user") None false PromptAuthority.empty

        Assert.Equal(PromptAuthority.UnknownOrigin, origin)

    [<Fact>]
    let ``New authority root replaces profile while continuation does not`` () =
        let session = SessionId.create "s1"

        let oldProfile =
            PromptAuthority.createAuthorityRoot
                session
                PromptAuthority.HumanRoot
                (MessageId.create "root-a")
                "manager"
                None
                None

        let newProfile =
            PromptAuthority.createAuthorityRoot
                session
                PromptAuthority.HumanRoot
                (MessageId.create "root-b")
                "coder"
                None
                None

        let projection = PromptAuthority.registerAuthority oldProfile PromptAuthority.empty
        let projection = PromptAuthority.registerAuthority newProfile projection

        Assert.Equal(Some newProfile, projection.LastAuthorityProfile)
        Assert.True(oldProfile.LogicalRunId <> newProfile.LogicalRunId)
