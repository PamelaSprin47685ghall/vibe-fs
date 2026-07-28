namespace Wanxiangshu.Next.Tests.MockOpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Domain

module FallbackModelSelectionTests =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    /// 0.5.0: model IDs are not resolved by Wanxiangshu. Effective agent
    /// selection from a SelectedAgent/PeerAgent pair follows A/A/B/B forever.
    let ``EffectiveAgent follows A A B B then wraps to A`` () =
        task {
            let authority =
                { AgentPairCursor.AuthorityAgentPair.SelectedAgent = "fast-inspector"
                  AgentPairCursor.AuthorityAgentPair.PeerAgent = "deep-inspector" }

            let mutable cursor = AgentPairCursor.initial

            let selected () =
                AgentPairCursor.effectiveAgent authority cursor

            equal "fast-inspector" (selected ())

            cursor <- AgentPairCursor.advanceCursor cursor 1L
            equal "fast-inspector" (selected ())

            cursor <- AgentPairCursor.advanceCursor cursor 2L
            equal "deep-inspector" (selected ())

            cursor <- AgentPairCursor.advanceCursor cursor 3L
            equal "deep-inspector" (selected ())

            // 4th advance wraps offset 3→0 (A again — infinite A/A/B/B cycle).
            cursor <- AgentPairCursor.advanceCursor cursor 4L
            equal "fast-inspector" (selected ())

            return ()
        }
