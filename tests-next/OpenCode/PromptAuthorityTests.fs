namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

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
            PromptAuthority.createAuthorityRoot "rt-test" session PromptAuthority.HumanRoot root "manager" None None

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
                PromptAuthority.createAuthorityRoot "rt-test" session PromptAuthority.HumanRoot root "manager" None None

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
                "rt-test"
                session
                PromptAuthority.HumanRoot
                (MessageId.create "root-a")
                "manager"
                None
                None

        let newProfile =
            PromptAuthority.createAuthorityRoot
                "rt-test"
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

    [<Fact>]
    let ``Chat_message_maps_keyed_continuation_without_becoming_human_root`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "rt-authority") 1 DateTimeOffset.UtcNow

                let session = SessionId.create "s-auth"
                let humanRoot = MessageId.create "human-root-1"

                let profile =
                    PromptAuthority.createAuthorityRoot
                        "rt-authority"
                        session
                        PromptAuthority.HumanRoot
                        humanRoot
                        "manager"
                        None
                        None

                match
                    AgentJournal.appendAgent
                        (StreamId.Session session)
                        (Some(TurnId.ofMessageId humanRoot))
                        (AgentFact.AuthorityRootAccepted
                            {| SessionId = session
                               LogicalRunId = profile.LogicalRunId
                               HostMessageId = MessageId.value humanRoot
                               AuthorityKind = "HumanRoot"
                               Agent = "manager"
                               BaseProviderID = None
                               BaseModelID = None
                               Variant = None |})
                        journal
                with
                | Error e -> Assert.True(false, sprintf "authority root failed: %A" e)
                | Ok _ -> ()

                match
                    AgentJournal.appendAgent
                        (StreamId.Session session)
                        None
                        (AgentFact.PluginPromptClaimed
                            {| PromptKey = "pk-repair-1"
                               SessionId = session
                               LogicalRunId = profile.LogicalRunId
                               AuthorityRootUserMessageId = MessageId.value humanRoot
                               ContinuationKind = "InteractionRepair"
                               Agent = Some "manager"
                               EffectiveProviderID = None
                               EffectiveModelID = None
                               Variant = None |})
                        journal
                with
                | Error e -> Assert.True(false, sprintf "claim failed: %A" e)
                | Ok _ -> ()

                let roles = System.Collections.Generic.Dictionary<string, string>()
                let bound = ResizeArray<string * string>()

                let hookObj =
                    HostSignalChatMessage.createHook
                        (Some journal)
                        roles
                        (fun sid mid -> bound.Add(("root", sid + ":" + mid)))
                        (fun sid mid -> bound.Add(("cont", sid + ":" + mid)))
                        (fun _ -> ())
                        None

                let hook = unbox<obj -> obj -> unit> hookObj

                let input =
                    createObj
                        [ "sessionID", box "s-auth"
                          "messageID", box "physical-repair-1"
                          "agent", box "manager"
                          "metadata",
                          box (
                              createObj
                                  [ "wanxiangshu_prompt_key", box "pk-repair-1"
                                    "wanxiangshu_origin", box "InteractionRepair" ]
                          ) ]

                hook input (createObj [ "message", createObj [ "id", box "physical-repair-1" ] ])

                let projection =
                    (AgentJournal.snapshot journal).AgentProjections.Sessions
                    |> Map.find session
                    |> fun s -> s.PromptAuthority

                match projection with
                | None -> Assert.True(false, "missing authority projection")
                | Some proj ->
                    Assert.Equal(
                        Some(MessageId.value humanRoot),
                        proj.LastAuthorityProfile
                        |> Option.map (fun p -> p.AuthorityRootUserMessageId)
                    )

                    Assert.True(proj.AcceptedContinuationIds.ContainsKey "physical-repair-1")
                    Assert.False(proj.AcceptedContinuationIds.ContainsKey "accepted-s-auth")
                    Assert.True(bound.Exists(fun (kind, _) -> kind = "cont"))
                    Assert.False(bound.Exists(fun (kind, pair) -> kind = "root" && pair.Contains "physical-repair-1"))
            })

    [<Fact>]
    let ``Stable logical run id is deterministic for same host message`` () =
        let session = SessionId.create "s1"
        let root = MessageId.create "human-root"
        let a =
            PromptAuthority.createAuthorityRoot "rt" session PromptAuthority.HumanRoot root "manager" None None
        let b =
            PromptAuthority.createAuthorityRoot "rt" session PromptAuthority.HumanRoot root "manager" None None
        Assert.Equal(a.LogicalRunId, b.LogicalRunId)
        Assert.True(a.LogicalRunId.Length = 64)

    [<Fact>]
    let ``Interaction repair identity is claimed only once`` () =
        let session = SessionId.create "s1"
        let root = MessageId.create "human-root"
        let profile =
            PromptAuthority.createAuthorityRoot "rt" session PromptAuthority.HumanRoot root "manager" None None
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

