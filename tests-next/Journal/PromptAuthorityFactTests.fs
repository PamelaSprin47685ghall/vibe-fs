namespace Wanxiangshu.Next.Tests.JournalTests

open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module PromptAuthorityFactTests =

    [<Fact>]
    let ``Prompt authority continuation never replaces authority root`` () =
        let sessionId = SessionId.create "authority-session"

        let root =
            AgentFact.AuthorityRootAccepted
                {| SessionId = sessionId
                   LogicalRunId = "run-1"
                   HostMessageId = "human-root"
                   AuthorityKind = "HumanRoot"
                   SelectedAgent = "fast-manager"
                   PeerAgent = "deep-manager"
                   CanonicalRole = "manager"
                   SelectedTier = "Fast" |}

        let claim =
            AgentFact.PluginPromptClaimed
                {| PromptKey = "pk-1"
                   SessionId = sessionId
                   LogicalRunId = "run-1"
                   AuthorityRootUserMessageId = "human-root"
                   ContinuationKind = "InteractionRepair"
                   EffectiveAgent = Some "fast-manager" |}

        let accepted =
            AgentFact.PluginPromptAccepted
                {| PromptKey = "pk-1"
                   SessionId = sessionId
                   HostMessageId = "repair-physical" |}

        let projection =
            AgentFacts.empty
            |> fun projection -> AgentFacts.foldAgentFact projection root
            |> fun projection -> AgentFacts.foldAgentFact projection claim
            |> fun projection -> AgentFacts.foldAgentFact projection accepted

        let authority = projection.Sessions.[sessionId].PromptAuthority |> Option.get

        Assert.Equal(
            Some "human-root",
            authority.LastAuthorityProfile
            |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)
        )

        Assert.equal (
            Some "fast-manager",
            authority.LastAuthorityProfile
            |> Option.map (fun profile -> profile.SelectedAgent)
        )

        Assert.equal (
            Some "deep-manager",
            authority.LastAuthorityProfile |> Option.map (fun profile -> profile.PeerAgent)
        )

        Assert.equal (
            Some "manager",
            authority.LastAuthorityProfile
            |> Option.map (fun profile -> profile.CanonicalRole)
        )

        Assert.equal (
            Some "Fast",
            authority.LastAuthorityProfile
            |> Option.map (fun profile -> profile.SelectedTier)
        )

        Assert.equal (Some "InteractionRepair", Map.tryFind "repair-physical" authority.AcceptedContinuationIds)
        Assert.Empty(authority.PendingClaims)
