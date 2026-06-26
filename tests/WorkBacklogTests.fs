module Wanxiangshu.Tests.WorkBacklogTests

open Wanxiangshu.Tests.BacklogReplaySpecs
open Wanxiangshu.Tests.BacklogReplaySpecsMimocode
open Wanxiangshu.Tests.BacklogReplaySpecsFold
open Wanxiangshu.Tests.BacklogProjectionSpecs


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
