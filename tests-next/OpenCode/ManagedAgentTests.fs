namespace Wanxiangshu.Next.Tests.OpenCode

open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.OpenCode

module ManagedAgentTests =

    let private expectOk name =
        match ManagedAgent.parse name with
        | Ok agent -> agent
        | Error err -> failwithf "expected Ok for %s but got %A" name err

    let private expectError name =
        match ManagedAgent.parse name with
        | Error err -> err
        | Ok agent -> failwithf "expected Error for %s but got %A" name agent

    [<Fact>]
    let ``required_names_are_exactly_20_fast_deep_pairs`` () =
        Assert.equal (20, List.length ManagedAgent.requiredNames)

        for role in ManagedAgent.allRoles do
            Assert.True(List.contains (ManagedAgent.nameOf AgentTier.Fast role) ManagedAgent.requiredNames)
            Assert.True(List.contains (ManagedAgent.nameOf AgentTier.Deep role) ManagedAgent.requiredNames)

    [<Fact>]
    let ``public_and_internal_parse_and_peer_roundtrip`` () =
        for role in ManagedAgent.allPublicRoles do
            let fast = ManagedAgent.make AgentTier.Fast role
            let deep = ManagedAgent.make AgentTier.Deep role
            Assert.equal (AgentVisibility.Public, fast.Visibility)
            Assert.equal (AgentVisibility.Public, deep.Visibility)
            Assert.equal (fast.Name, (ManagedAgent.peer deep).Name)
            Assert.equal (deep.Name, (ManagedAgent.peer fast).Name)
            Assert.equal (fast.Name, (ManagedAgent.peer (ManagedAgent.peer fast)).Name)
            Assert.equal (role, (expectOk fast.Name).Role)
            Assert.equal (role, (expectOk deep.Name).Role)

        for role in ManagedAgent.allInternalRoles do
            let fast = ManagedAgent.make AgentTier.Fast role
            let deep = ManagedAgent.make AgentTier.Deep role
            Assert.equal (AgentVisibility.Internal, fast.Visibility)
            Assert.equal (AgentVisibility.Internal, deep.Visibility)
            Assert.equal (role, (expectOk fast.Name).Role)
            Assert.equal (role, (expectOk deep.Name).Role)

    [<Fact>]
    let ``legacy_and_malformed_names_are_rejected`` () =
        match expectError "reviewer" with
        | ManagedAgentParseError.LegacyAgentName "reviewer" -> ()
        | other -> failwithf "expected LegacyAgentName reviewer, got %A" other

        match expectError "build" with
        | ManagedAgentParseError.LegacyAgentName "build" -> ()
        | other -> failwithf "expected LegacyAgentName build, got %A" other

        match expectError "manager" with
        | ManagedAgentParseError.LegacyAgentName "manager" -> ()
        | other -> failwithf "expected LegacyAgentName manager, got %A" other

        match expectError "fast_reviewer" with
        | ManagedAgentParseError.LegacyAgentName _ -> ()
        | other -> failwithf "expected legacy underscore rejection, got %A" other

        match expectError "reviewer-fast" with
        | ManagedAgentParseError.LegacyAgentName _ -> ()
        | other -> failwithf "expected legacy suffix-order rejection, got %A" other

        match expectError "deep-inspecter" with
        | ManagedAgentParseError.UnknownManagedAgent "deep-inspecter" -> ()
        | other -> failwithf "expected UnknownManagedAgent deep-inspecter, got %A" other

        match expectError "FAST-reviewer" with
        | ManagedAgentParseError.UnknownManagedAgent _ -> ()
        | other -> failwithf "expected case-sensitive rejection, got %A" other

        Assert.True((ManagedAgent.tryParse "fast-coder").IsSome)
        Assert.True((ManagedAgent.tryParse "coder").IsNone)
        Assert.True((ManagedAgent.tryParse "plan").IsNone)

    [<Fact>]
    let ``effective_agent_follows_infinite_AABB_from_selected_tier`` () =
        let seq12 =
            EffectiveAgentResolver.sideSequence 12
            |> List.map (function
                | EffectiveAgentResolver.ModelSide.SideA -> "A"
                | EffectiveAgentResolver.ModelSide.SideB -> "B")

        Assert.equal ([ "A"; "A"; "B"; "B"; "A"; "A"; "B"; "B"; "A"; "A"; "B"; "B" ], seq12)

        let selected = ManagedAgent.make AgentTier.Deep Role.Inspector
        let cursor0 = EffectiveAgentResolver.initialCursor
        Assert.equal ("deep-inspector", EffectiveAgentResolver.effectiveAgentFromManaged selected cursor0)

        let cursor2 = { cursor0 with Offset = 2uy }
        Assert.equal ("fast-inspector", EffectiveAgentResolver.effectiveAgentFromManaged selected cursor2)

        let cursor3 = { cursor0 with Offset = 3uy }
        let after = EffectiveAgentResolver.advanceCursor cursor3 99L
        Assert.equal (0uy, after.Offset)
        Assert.equal ("deep-inspector", EffectiveAgentResolver.effectiveAgentFromManaged selected after)
