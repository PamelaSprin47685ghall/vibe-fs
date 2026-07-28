namespace Wanxiangshu.Next.Tests.Domain

open Xunit
open Wanxiangshu.Next.Domain

module AgentPairCursorTests =

    [<Fact>]
    let ``initial_cursor_is_side_A`` () =
        let cursor = AgentPairCursor.initial
        Assert.equal (0uy, cursor.Offset)
        Assert.equal (AgentPairCursor.ModelSide.SideA, AgentPairCursor.side cursor.Offset)

    [<Fact>]
    let ``advance_cycles_A_A_B_B_without_death`` () =
        let expected =
            [ AgentPairCursor.ModelSide.SideA
              AgentPairCursor.ModelSide.SideA
              AgentPairCursor.ModelSide.SideB
              AgentPairCursor.ModelSide.SideB
              AgentPairCursor.ModelSide.SideA
              AgentPairCursor.ModelSide.SideA
              AgentPairCursor.ModelSide.SideB
              AgentPairCursor.ModelSide.SideB
              AgentPairCursor.ModelSide.SideA
              AgentPairCursor.ModelSide.SideA
              AgentPairCursor.ModelSide.SideB
              AgentPairCursor.ModelSide.SideB ]

        let mutable cursor = AgentPairCursor.initial

        for expectedSide in expected do
            Assert.equal (expectedSide, AgentPairCursor.side cursor.Offset)
            cursor <- AgentPairCursor.advanceCursor cursor ((int cursor.Offset + 1) |> int64)

    [<Fact>]
    let ``effectiveAgent_maps_AABB`` () =
        let authority =
            { AgentPairCursor.AuthorityAgentPair.SelectedAgent = "fast-inspector"
              AgentPairCursor.AuthorityAgentPair.PeerAgent = "deep-inspector" }

        let selected () =
            AgentPairCursor.effectiveAgent authority AgentPairCursor.initial

        Assert.equal ("fast-inspector", selected ())

        let c1 = AgentPairCursor.advanceCursor AgentPairCursor.initial 1L
        Assert.equal ("fast-inspector", AgentPairCursor.effectiveAgent authority c1)

        let c2 = AgentPairCursor.advanceCursor c1 2L
        Assert.equal ("deep-inspector", AgentPairCursor.effectiveAgent authority c2)

        let c3 = AgentPairCursor.advanceCursor c2 3L
        Assert.equal ("deep-inspector", AgentPairCursor.effectiveAgent authority c3)

        let c4 = AgentPairCursor.advanceCursor c3 4L
        Assert.equal ("fast-inspector", AgentPairCursor.effectiveAgent authority c4)
