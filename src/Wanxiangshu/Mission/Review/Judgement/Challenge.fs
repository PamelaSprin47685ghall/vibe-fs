namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Foundation

/// Provider-visible skeptical challenge. Its bytes are presentation only;
/// Finality causality is owned by ReviewBarrierWorkflow's typed physical edge.
[<RequireQualifiedAccess>]
module ReviewChallenge =

    let Path = "review/challenge"

    let promptOf (text: string) : string = SyntheticToml.document [ text ] []
