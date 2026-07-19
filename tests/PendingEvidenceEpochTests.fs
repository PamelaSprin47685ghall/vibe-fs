module Wanxiangshu.Tests.PendingEvidenceEpochTests

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionPendingEvidence
open Wanxiangshu.Tests.Assert

let private evidence (text: string) : CurrentTurnEvidence =
    { CurrentTurnEvidence.empty with
        Outcome = CompletionRequested text }

let run () : unit =
    let session = "pending-evidence-epoch-tests"

    let preRunSession = "pending-evidence-pre-run"
    SubsessionPendingEvidence.BufferPreRun preRunSession (evidence "arrived-before-begin")
    let preRunEpoch = SubsessionPendingEvidence.BeginRun preRunSession

    check
        "evidence before BeginRun is discarded, not bound to the new epoch"
        (SubsessionPendingEvidence.TakeAllEpoch preRunSession preRunEpoch |> List.isEmpty)

    SubsessionPendingEvidence.EndRun preRunSession preRunEpoch

    for epoch in [ 0; 33; 100; 1000 ] do
        SubsessionPendingEvidence.BufferEpoch session epoch (evidence (string epoch))

    for epoch in [ 0; 33; 100; 1000 ] do
        check
            (sprintf "epoch %d evidence remains addressable" epoch)
            (SubsessionPendingEvidence.TakeAllEpoch session epoch |> List.length = 1)

    SubsessionPendingEvidence.BufferEpoch session 0 (evidence "closed")
    SubsessionPendingEvidence.BufferEpoch session 1000 (evidence "closed-high")
    SubsessionPendingEvidence.ForgetSession session

    check "session close removes epoch zero" (SubsessionPendingEvidence.TakeAllEpoch session 0 |> List.isEmpty)
    check "session close removes high epochs" (SubsessionPendingEvidence.TakeAllEpoch session 1000 |> List.isEmpty)

    let activeSession = "pending-evidence-active-epoch"
    let epoch = SubsessionPendingEvidence.BeginRun activeSession

    check
        "new epoch starts without evidence"
        (SubsessionPendingEvidence.TakeAllEpoch activeSession epoch |> List.isEmpty)

    SubsessionPendingEvidence.BufferPreRun activeSession (evidence "arrived-during-begin")

    check
        "evidence during BeginRun remains on that epoch"
        (SubsessionPendingEvidence.TakeAllEpoch activeSession epoch |> List.length = 1)

    SubsessionPendingEvidence.EndRun activeSession epoch
    SubsessionPendingEvidence.BufferPreRun activeSession (evidence "stale-from-previous-run")
    let nextEpoch = SubsessionPendingEvidence.BeginRun activeSession

    check "next run receives a new epoch" (nextEpoch > epoch)

    check
        "next run does not receive cross-turn stale evidence"
        (SubsessionPendingEvidence.TakeAllEpoch activeSession nextEpoch |> List.isEmpty)

    SubsessionPendingEvidence.EndRun activeSession nextEpoch
