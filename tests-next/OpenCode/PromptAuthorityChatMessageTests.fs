namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// Host drops top-level prompt metadata. chat.message must still accept
/// ReviewConfirmation as continuation, never a new HumanRoot.
module PromptAuthorityChatMessageTests =

    type private AdmissionPort() =
        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                // Keep claim pending until chat.message (host admission id).
                Task.FromResult(Ok(MessageId.create "accepted-s-reviewer"))

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

    let private seedReviewer (journal: AgentJournal) runtime sessionId humanRoot =
        let profile =
            expectAuthorityRoot runtime sessionId PromptAuthority.HumanRoot humanRoot "deep-reviewer"

        Assert.equal ("deep-reviewer", profile.SelectedAgent)
        Assert.equal ("fast-reviewer", profile.PeerAgent)

        let svc = PromptDispatcher.forJournal journal
        svc.RegisterAuthority profile

        task {
            let! claimed =
                svc.SendContinuation
                    (AdmissionPort() :> ISessionHostPort)
                    sessionId
                    "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"
                    PromptAuthority.ReviewConfirmation
                    profile
                    (PromptAuthority.selectedEffectiveAgent profile)
                    None
                    None

            match claimed with
            | Error e -> Assert.True(false, sprintf "claim send failed: %s" e)
            | Ok _ -> ()

            return svc, profile
        }

    let private assertContinuation
        (journal: AgentJournal)
        session
        humanRoot
        messageId
        (bound: ResizeArray<string * string>)
        =
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

            Assert.equal (Some "deep-reviewer", proj.LastAuthorityProfile |> Option.map (fun p -> p.SelectedAgent))

            Assert.equal (Some "fast-reviewer", proj.LastAuthorityProfile |> Option.map (fun p -> p.PeerAgent))

            Assert.True(proj.AcceptedContinuationIds.ContainsKey messageId)
            Assert.True(bound.Exists(fun (kind, _) -> kind = "cont"))
            Assert.False(bound.Exists(fun (kind, _) -> kind = "root"))

    [<Fact>]
    let ``Chat_message_recovers_prompt_key_from_text_part_metadata`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "rt-part-meta") 1 DateTimeOffset.UtcNow

                let session = SessionId.create "s-reviewer"
                let humanRoot = MessageId.create "reviewer-root-1"
                let! svc, _ = seedReviewer journal "rt-part-meta" session humanRoot

                let pendingKey =
                    svc.Projection.PendingClaims
                    |> Map.toList
                    |> List.map (fun (k, _) -> PromptKeyRef.value k)
                    |> List.head

                let roles = System.Collections.Generic.Dictionary<string, string>()
                let bound = ResizeArray<string * string>()

                let hook =
                    unbox<obj -> obj -> unit> (
                        HostSignalChatMessage.createHook
                            (Some journal)
                            roles
                            (fun sid mid -> bound.Add(("root", sid + ":" + mid)))
                            (fun sid mid -> bound.Add(("cont", sid + ":" + mid)))
                            (fun _ -> ())
                    )

                let output =
                    createObj
                        [ "message", createObj [ "id", box "physical-confirm-1"; "agent", box "deep-reviewer" ]
                          "parts",
                          box
                              [| createObj
                                     [ "type", box "text"
                                       "text", box "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"
                                       "metadata",
                                       createObj
                                           [ "wanxiangshu_prompt_key", box pendingKey
                                             "wanxiangshu_origin", box "ReviewConfirmation" ] ] |] ]

                hook
                    (createObj
                        [ "sessionID", box "s-reviewer"
                          "messageID", box "physical-confirm-1"
                          "agent", box "deep-reviewer" ])
                    output

                assertContinuation journal session humanRoot "physical-confirm-1" bound
            })

    [<Fact>]
    let ``Chat_message_accepts_single_pending_claim_without_transport_metadata`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "rt-pending-only") 1 DateTimeOffset.UtcNow

                let session = SessionId.create "s-confirm"
                let humanRoot = MessageId.create "root-pending"
                let! _, _ = seedReviewer journal "rt-pending-only" session humanRoot

                let roles = System.Collections.Generic.Dictionary<string, string>()
                let bound = ResizeArray<string * string>()

                let hook =
                    unbox<obj -> obj -> unit> (
                        HostSignalChatMessage.createHook
                            (Some journal)
                            roles
                            (fun sid mid -> bound.Add(("root", sid + ":" + mid)))
                            (fun sid mid -> bound.Add(("cont", sid + ":" + mid)))
                            (fun _ -> ())
                    )

                hook
                    (createObj
                        [ "sessionID", box "s-confirm"
                          "messageID", box "physical-confirm-bare"
                          "agent", box "deep-reviewer" ])
                    (createObj
                        [ "message", createObj [ "id", box "physical-confirm-bare"; "agent", box "deep-reviewer" ]
                          "parts",
                          box [| createObj [ "type", box "text"; "text", box "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?" ] |] ])

                assertContinuation journal session humanRoot "physical-confirm-bare" bound
            })
