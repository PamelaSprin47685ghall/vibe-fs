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

module PromptAuthorityTests =

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
    let ``Continuation acceptance preserves last authority profile`` () =
        let session = SessionId.create "s1"
        let root = MessageId.create "human-root"

        let profile =
            expectAuthorityRoot "rt-test" session PromptAuthority.HumanRoot root "fast-manager"

        Assert.equal ("fast-manager", profile.SelectedAgent)
        Assert.equal ("deep-manager", profile.PeerAgent)
        Assert.equal (Role.Manager, profile.CanonicalRole)
        Assert.equal (AgentTier.Fast, profile.SelectedTier)

        let before = PromptAuthority.registerAuthority profile PromptAuthority.empty
        let key = PromptAuthority.newPromptKey ()

        let claim =
            PromptAuthority.claimContinuation
                key
                session
                PromptAuthority.InteractionRepair
                profile
                (PromptAuthority.selectedEffectiveAgent profile)

        let after =
            before
            |> PromptAuthority.registerClaim claim
            |> PromptAuthority.acceptClaim key (MessageId.create "repair-message")

        Assert.equal (Some profile, after.LastAuthorityProfile)
        Assert.equal (Some profile, after.ActiveLogicalRun)

        Assert.equal (
            Some PromptAuthority.InteractionRepair,
            Map.tryFind (MessageId.create "repair-message") after.AcceptedContinuationIds
        )

    [<Fact>]
    let ``Dispatcher claims continuation and preserves authority metadata`` () =
        task {
            let session = SessionId.create "s1"
            let root = MessageId.create "human-root"

            let profile =
                expectAuthorityRoot "rt-test" session PromptAuthority.HumanRoot root "fast-manager"

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
                    (PromptAuthority.selectedEffectiveAgent profile)
                    None
                    None

            Assert.equal (Ok(MessageId.create "continuation-1"), accepted)
            Assert.equal (Some profile, dispatcher.Projection.LastAuthorityProfile)

            match port.Options with
            | Some { Metadata = Some metadata
                     Agent = Some agent } ->
                Assert.NotNull(metadata?wanxiangshu_prompt_key)
                Assert.equal ("InteractionRepair", unbox<string> metadata?wanxiangshu_origin)
                Assert.equal ("human-root", unbox<string> metadata?wanxiangshu_authority_root)
                Assert.equal ("fast-manager", agent)
                Assert.True(port.Options.Value.Model.IsNone)
            | _ -> Assert.True(false, "dispatcher omitted continuation metadata")
        }

    [<Fact>]
    let ``Unknown physical user message never becomes authority`` () =
        let origin =
            PromptAuthority.resolveKnownOrigin (MessageId.create "unproven-user") None false PromptAuthority.empty

        Assert.equal (PromptAuthority.UnknownOrigin, origin)

    [<Fact>]
    let ``New authority root replaces profile while continuation does not`` () =
        let session = SessionId.create "s1"

        let oldProfile =
            expectAuthorityRoot "rt-test" session PromptAuthority.HumanRoot (MessageId.create "root-a") "fast-manager"

        let newProfile =
            expectAuthorityRoot "rt-test" session PromptAuthority.HumanRoot (MessageId.create "root-b") "fast-coder"

        Assert.equal ("fast-coder", newProfile.SelectedAgent)
        Assert.equal ("deep-coder", newProfile.PeerAgent)

        let projection = PromptAuthority.registerAuthority oldProfile PromptAuthority.empty
        let projection = PromptAuthority.registerAuthority newProfile projection

        Assert.equal (Some newProfile, projection.LastAuthorityProfile)
        Assert.True(oldProfile.LogicalRunId <> newProfile.LogicalRunId)

    [<Fact>]
    let ``createAuthorityRoot rejects bare legacy agent names`` () =
        match
            PromptAuthority.createAuthorityRoot
                "rt-test"
                (SessionId.create "s1")
                PromptAuthority.HumanRoot
                (MessageId.create "human-root")
                "manager"
        with
        | Error _ -> ()
        | Ok _ -> Assert.True(false, "bare manager must be rejected")

    [<Fact>]
    let ``Chat_message_maps_keyed_continuation_without_becoming_human_root`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "rt-authority") 1 DateTimeOffset.UtcNow

                let session = SessionId.create "s-auth"
                let humanRoot = MessageId.create "human-root-1"

                let profile =
                    expectAuthorityRoot "rt-authority" session PromptAuthority.HumanRoot humanRoot "fast-manager"

                match
                    AgentJournal.appendAgent
                        (StreamId.Session session)
                        (Some(TurnId.ofMessageId humanRoot))
                        (AgentFact.AuthorityRootAccepted
                            {| SessionId = session
                               LogicalRunId = profile.LogicalRunId
                               HostMessageId = MessageId.value humanRoot
                               AuthorityKind = "HumanRoot"
                               SelectedAgent = profile.SelectedAgent
                               PeerAgent = profile.PeerAgent
                               CanonicalRole = PromptAuthority.roleLabel profile.CanonicalRole
                               SelectedTier = PromptAuthority.tierLabel profile.SelectedTier |})
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
                               EffectiveAgent = Some(PromptAuthority.selectedEffectiveAgent profile) |})
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

                let hook = unbox<obj -> obj -> unit> hookObj

                let input =
                    createObj
                        [ "sessionID", box "s-auth"
                          "messageID", box "physical-repair-1"
                          "agent", box "fast-manager"
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
                    Assert.equal (
                        Some(MessageId.value humanRoot),
                        proj.LastAuthorityProfile |> Option.map (fun p -> p.AuthorityRootUserMessageId)
                    )

                    Assert.equal (
                        Some "fast-manager",
                        proj.LastAuthorityProfile |> Option.map (fun p -> p.SelectedAgent)
                    )

                    Assert.equal (Some "deep-manager", proj.LastAuthorityProfile |> Option.map (fun p -> p.PeerAgent))

                    Assert.True(proj.AcceptedContinuationIds.ContainsKey "physical-repair-1")
                    Assert.False(proj.AcceptedContinuationIds.ContainsKey "accepted-s-auth")
                    Assert.True(bound.Exists(fun (kind, _) -> kind = "cont"))
                    Assert.False(bound.Exists(fun (kind, pair) -> kind = "root" && pair.Contains "physical-repair-1"))
            })
