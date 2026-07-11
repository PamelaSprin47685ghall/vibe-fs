module Wanxiangshu.Tests.ArchitectureTestsFoundationB

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let noQuadraticListAppend () =
    let scanDirs =
        [ "src/Kernel"
          "src/Shell"
          "src/Mux"
          "src/Opencode"
          "src/Omp"
          "src/Methodology" ]

    let allFiles =
        scanDirs
        |> List.collect (fun d -> fsFilesRecursive d)
        |> Seq.filter (fun p -> p.EndsWith(".fs"))

    let allowedAppends =
        Set
            [ "src/Kernel/FuzzyFormat.fs"
              "src/Kernel/FuzzyPath.fs"
              "src/Kernel/ReviewSession/Types.fs"
              "src/Kernel/SubagentPrompts.fs"
              "src/Shell/OpencodeAgentConfigCodec.fs"
              "src/Mux/AiSettings.fs" ]

    for path in allFiles do
        if not (Set.contains path allowedAppends) then
            let code = requireFile path |> nonCommentCode
            let hasAppend = code.Contains "@ [" || code.Contains "@ ("
            check ($"arch: {path} has no quadratic list append") (not hasAppend)

let parallelToolPromptSSOTGuard () =
    let content = requireFile "src/Shell/MessageTransformPipeline.fs"
    let hostTools = requireFile "src/Kernel/HostTools.fs"

    check
        "arch: MessageTransformPipeline uses HostTools.isSynthCallId"
        (content.Contains "Wanxiangshu.Kernel.HostTools.isSynthCallId")

    check
        "arch: MessageTransformPipeline does not hardcode tool exclude list"
        (not (content.Contains "let catalogNames ="))

    check "arch: HostTools defines synthCallIdPrefixes" (hostTools.Contains "let synthCallIdPrefixes")

    check "arch: HostTools excludes semble-call- from trigger" (hostTools.Contains "\"semble-call-\"")

    check "arch: HostTools excludes caps-call- from trigger" (hostTools.Contains "\"caps-call-\"")

let wanxiangzhenBoundary () =
    for f in fsFilesRecursive "src/Kernel/Wanxiangzhen" do
        let content = requireFile f
        check ($"arch: {f} Dyn-free") (not (content.Contains "Dyn."))
        check ($"arch: {f} no open Shell") (not (content.Contains "open Wanxiangshu.Shell"))
        check ($"arch: {f} no [<Emit>]") (not (content.Contains "[<Emit"))

let wanxiangzhenGitQueue () =
    let ops = requireFile "src/Shell/Wanxiangzhen/CoordinatorOps.fs" |> nonCommentCode
    check "arch: CoordinatorOps uses rt.GitQueue" (ops.Contains "rt.GitQueue.Enqueue")
    check "arch: CoordinatorOps uses rt.DagQueue" (ops.Contains "rt.DagQueue.Enqueue")

let squadEventFoldUsesTransitionPolicy () =
    let squadEvent =
        requireFile "src/Kernel/Wanxiangzhen/SquadEvent.fs" |> nonCommentCode

    check
        "arch: SquadEvent.fold uses TransitionPolicy"
        (squadEvent.Contains "SquadTaskTransition" && squadEvent.Contains "ReplayFact")

let sessionGateNoBoolTriple () =
    let sessionLoop = requireFile "src/Kernel/SessionLoop.fs" |> nonCommentCode
    let recovery = requireFile "src/Shell/FallbackRecoveryWait.fs" |> nonCommentCode

    check "arch: SessionLoop has no gateModeFromFlags" (not (sessionLoop.Contains "gateModeFromFlags"))
    check "arch: FallbackRecoveryWait has no gateModeFromFlags" (not (recovery.Contains "gateModeFromFlags"))
    check "arch: SessionLoop uses gateModeFromDemand" (sessionLoop.Contains "gateModeFromDemand")

let fallbackRuntimeNoBoolGateMaps () =
    let rt = requireFile "src/Shell/FallbackRuntimeState.fs" |> nonCommentCode

    check "arch: no nudgeActive bool map" (not (rt.Contains "nudgeActive <-"))
    check "arch: uses activeGates" (rt.Contains "activeGates")
    let gates = requireFile "src/Shell/FallbackRuntimeStateGates.fs" |> nonCommentCode

    check "arch: uses FallbackConsumedStatus" (rt.Contains "emptyConsumed" && gates.Contains "FallbackConsumedStatus")

let nudgeSnapshotSourceNoBoolInput () =
    let deriv = requireFile "src/Kernel/Nudge/NudgeDerivation.fs" |> nonCommentCode

    check "arch: no SnapshotInput" (not (deriv.Contains "SnapshotInput"))
    check "arch: NudgeSnapshotSource" (deriv.Contains "NudgeSnapshotSource")

let nudgeHostsUseSessionSnapshotFromFold () =
    for path in
        [ "src/Opencode/NudgeEffect.fs"
          "src/Omp/NudgeHooks.fs"
          "src/Shell/NudgeRuntimeMux.fs" ] do
        let code = requireFile path |> nonCommentCode
        check ($"arch: {path} uses sessionSnapshotFromFold") (code.Contains "sessionSnapshotFromFold")

let fallbackLifecycleAdt () =
    let rt = requireFile "src/Shell/FallbackRuntimeState.fs" |> nonCommentCode

    check "arch: ApplyContinueMode" (rt.Contains "ApplyContinueMode")
    check "arch: ApplyTaskCompletion" (rt.Contains "ApplyTaskCompletion")

let nudgeWorkStateAdt () =
    let types = requireFile "src/Kernel/Nudge/Types.fs" |> nonCommentCode

    check "arch: no RunnerActive bool tuple" (not (types.Contains "RunnerActive"))
    check "arch: workStateFromAxes present" (types.Contains "workStateFromAxes")

let reviewLoopFoldAdt () =
    let fold = requireFile "src/Kernel/EventLog/Fold.fs" |> nonCommentCode

    check
        "arch: EventLog fold uses ReviewLoopFold ADT"
        (fold.Contains "ReviewLoopFold" && fold.Contains "foldReviewLoop")

let coordinatorReplayUsesTransitionPolicy () =
    let replay =
        requireFile "src/Shell/Wanxiangzhen/CoordinatorReplay.fs" |> nonCommentCode

    check
        "arch: CoordinatorReplay uses SquadTaskTransition"
        (replay.Contains "SquadTaskTransition"
         && replay.Contains "applyStatus ReplayFact")

let wanxiangzhenReconcile () =
    let replay =
        requireFile "src/Shell/Wanxiangzhen/CoordinatorReplay.fs" |> nonCommentCode

    check
        "arch: CoordinatorReplay Submitted to Running via ReplayFact"
        (replay.Contains "Submitted" && replay.Contains "Running")

    check
        "arch: CoordinatorReplay does not reconcile Cancelled to Merged"
        (not (replay.Contains "Cancelled" && replay.Contains "applyStatus ReplayFact t Merged"))
