module VibeFs.Tests.WorkBacklogTests

open VibeFs.Tests.BacklogReplaySpecs
open VibeFs.Tests.BacklogReplaySpecsMimocode
open VibeFs.Tests.BacklogReplaySpecsFold
open VibeFs.Tests.BacklogProjectionSpecs


let run () =
    replayBacklogOpencodeFallsBackToCapturedReportWhenInputMissing ()
    replayBacklogMuxFallsBackToCapturedReportWhenInputMissing ()
    replayBacklogOpencodeDoesNotMergeConsecutiveTodoWrite ()
    replayBacklogTest ()
    replayEmpty ()
    replaySkipsEmpty ()
    replayBacklogForMimocodeUsesTask ()
    replayBacklogForMimocodeIgnoresActor ()
    backlogSessionCaptureRoundTrip ()
    backlogSessionShareReportTableAcrossInstances ()
    backlogSessionRestoresMimocodeReportDuringBacklogRebuild ()
    replayBacklogForMimocodeMergesConsecutiveWorkReports ()
    replayBacklogForMimocodeMergesConsecutiveTaskBurst ()
    replayBacklogForMimocodeSplitsBurstsOnGap ()
    replayBacklogForMimocodeIgnoresAssistantTextBetweenTasks ()
    replayBacklogForMimocodeIgnoresReasoningBetweenTasks ()
    replayBacklogForMimocodeSplitsOnOtherToolCall ()
    findFoldRangeTest ()
    findFoldRangeOpencodePerCallMimicodePerBurst ()
    findFoldRangeForMimocodeIgnoresReadOnlyTaskCalls ()
    findFoldRangeForMimocodeRequiresThreeProgressBursts ()
    findFoldRangeForMimocodeUsesLastProgressCallInBurst ()
    findFoldRangeForMimocodeAssistantTextKeepsBurst ()
    projectBacklogFolds ()
    projectBacklogNoFold ()
    projectBacklogForMimocodeUsesTask ()
    projectBacklogHidesErrors ()
    projectBacklogDropsFoldedUserMessages ()
    projectBacklogKeepsReviewInFold ()
    projectBacklogPrefixUsesTodoTime ()
    projectBacklogPrefixStaysStableWhenGrowing ()
    backlogSessionRefreshesBacklog ()
    backlogSessionRefreshesBacklogForMimocode ()
    buildBacklogTextTest ()
    buildBacklogTextWithErrorTest ()
