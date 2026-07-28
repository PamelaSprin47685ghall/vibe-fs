namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module PromptAuthoritySendTests =

    type private CapturingPort(?returnId: string) =
        let mutable options: OpenCodePromptOptions option = None
        let responseId = defaultArg returnId "continuation-1"

        member _.Options = options

        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, opts) =
                options <- Some opts
                Task.FromResult(Ok(MessageId.create responseId))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(())
            member _.CreateChildSession(_, _) = Task.FromResult(Error "not used")
            member _.GetSessionOutput(_) = []

    let private expectAuthorityRoot runtime session kind messageId selectedAgent =
        match PromptAuthority.createAuthorityRoot runtime session kind messageId selectedAgent with
        | Ok profile -> profile
        | Error error ->
            Assert.True(false, sprintf "createAuthorityRoot failed: %s" error)
            failwith error

    [<Fact>]
    let ``AcceptHumanRoot builds managed agent profile without model fields`` () =
        let session = SessionId.create "s-human"
        let message = MessageId.create "human-root-2"
        let svc = PromptDispatcher.Dispatcher()

        match svc.AcceptHumanRoot session message (Some "fast-manager") None with
        | Error error -> Assert.True(false, sprintf "AcceptHumanRoot failed: %s" error)
        | Ok profile ->
            Assert.equal ("fast-manager", profile.SelectedAgent)
            Assert.equal ("deep-manager", profile.PeerAgent)
            Assert.equal (Role.Manager, profile.CanonicalRole)
            Assert.equal (AgentTier.Fast, profile.SelectedTier)
            Assert.equal (Some profile, svc.Projection.LastAuthorityProfile)
            Assert.equal (Some profile, svc.Projection.ActiveLogicalRun)

    [<Fact>]
    let ``SendAgentOwnerRoot sends EffectiveAgent with Model None`` () =
        task {
            let session = SessionId.create "s-owner"
            let port = CapturingPort("owner-root-1")
            let svc = PromptDispatcher.Dispatcher()

            let! accepted =
                svc.SendAgentOwnerRoot (port :> ISessionHostPort) session "owner task" "fast-coder" None None

            match accepted with
            | Error error -> Assert.True(false, sprintf "SendAgentOwnerRoot failed: %s" error)
            | Ok(messageId, profile) ->
                Assert.equal (MessageId.create "owner-root-1", messageId)
                Assert.equal ("fast-coder", profile.SelectedAgent)
                Assert.equal ("deep-coder", profile.PeerAgent)
                Assert.equal (Role.Coder, profile.CanonicalRole)
                Assert.equal (AgentTier.Fast, profile.SelectedTier)
                Assert.equal (PromptAuthority.AgentOwnerRoot, profile.AuthorityKind)

                match port.Options with
                | Some { Agent = Some agent; Model = model } ->
                    Assert.equal ("fast-coder", agent)
                    Assert.True(model.IsNone)
                | _ -> Assert.True(false, "SendAgentOwnerRoot omitted Agent=Some / Model=None shape")
        }

    [<Fact>]
    let ``Stable logical run id is deterministic for same host message`` () =
        let session = SessionId.create "s1"
        let root = MessageId.create "human-root"

        let a =
            expectAuthorityRoot "rt" session PromptAuthority.HumanRoot root "fast-manager"

        let b =
            expectAuthorityRoot "rt" session PromptAuthority.HumanRoot root "fast-manager"

        Assert.equal (a.LogicalRunId, b.LogicalRunId)
        Assert.True(a.LogicalRunId.Length = 64)

    [<Fact>]
    let ``Interaction repair identity is claimed only once`` () =
        let session = SessionId.create "s1"
        let root = MessageId.create "human-root"

        let profile =
            expectAuthorityRoot "rt" session PromptAuthority.HumanRoot root "fast-manager"

        let identity =
            PromptAuthority.repairIdentity
                profile.LogicalRunId
                profile.AuthorityRootUserMessageId
                (MessageId.create "asst-1")
                "zero-width"

        let first = PromptAuthority.tryClaimRepair identity PromptAuthority.empty
        Assert.True(first.IsSome)
        let second = PromptAuthority.tryClaimRepair identity first.Value
        Assert.True(second.IsNone)

    [<Fact>]
    let ``SendAgentOwnerRoot with accepted-* still accepts authority and sets ActiveLogicalRun`` () =
        task {
            let session = SessionId.create "s-owner-accepted"
            let port = CapturingPort("accepted-s-owner-accepted")
            let svc = PromptDispatcher.Dispatcher()

            let! accepted =
                svc.SendAgentOwnerRoot
                    (port :> ISessionHostPort)
                    session
                    "owner task accepted"
                    "fast-reviewer"
                    None
                    None

            match accepted with
            | Error error -> Assert.True(false, sprintf "SendAgentOwnerRoot failed: %s" error)
            | Ok(messageId, profile) ->
                Assert.equal (MessageId.create "accepted-s-owner-accepted", messageId)
                Assert.equal ("fast-reviewer", profile.SelectedAgent)
                Assert.equal ("deep-reviewer", profile.PeerAgent)
                Assert.equal (Role.Reviewer, profile.CanonicalRole)
                Assert.equal (AgentTier.Fast, profile.SelectedTier)

                Assert.True(svc.Projection.ActiveLogicalRun.IsSome, "ActiveLogicalRun missing after accepted-*")
                Assert.equal (Some profile, svc.Projection.ActiveLogicalRun)
                Assert.True(svc.Projection.PendingClaims |> Map.isEmpty, "PendingClaims should be consumed")

                match port.Options with
                | Some { Agent = Some agent; Model = model } ->
                    Assert.equal ("fast-reviewer", agent)
                    Assert.True(model.IsNone)
                | _ -> Assert.True(false, "SendAgentOwnerRoot omitted Agent=Some / Model=None shape")
        }

    [<Fact>]
    let ``AcceptAgentOwnerRoot is idempotent after SendAgentOwnerRoot accepted-*`` () =
        task {
            let session = SessionId.create "s-owner-accepted-2"
            let port = CapturingPort("accepted-s-owner-accepted-2")
            let svc = PromptDispatcher.Dispatcher()

            let! accepted =
                svc.SendAgentOwnerRoot
                    (port :> ISessionHostPort)
                    session
                    "owner task accepted"
                    "fast-reviewer"
                    None
                    None

            match accepted with
            | Error error -> Assert.True(false, sprintf "SendAgentOwnerRoot failed: %s" error)
            | Ok(_, profile) ->
                let promptKey =
                    match port.Options with
                    | Some { Metadata = Some metadata } ->
                        if isNull metadata?wanxiangshu_prompt_key then
                            failwith "missing wanxiangshu_prompt_key in metadata"
                        else
                            unbox<string> metadata?wanxiangshu_prompt_key
                    | _ -> failwith "missing metadata in prompt options"

                let realId = MessageId.create "real-owner-2"

                match svc.AcceptAgentOwnerRoot promptKey session realId with
                | Error error -> Assert.True(false, sprintf "AcceptAgentOwnerRoot should be idempotent: %s" error)
                | Ok profile2 ->
                    Assert.equal (profile.SelectedAgent, profile2.SelectedAgent)
                    Assert.equal (profile.PeerAgent, profile2.PeerAgent)
                    Assert.equal (profile.CanonicalRole, profile2.CanonicalRole)
                    Assert.True(svc.Projection.ActiveLogicalRun.IsSome, "ActiveLogicalRun missing after re-accept")
        }
