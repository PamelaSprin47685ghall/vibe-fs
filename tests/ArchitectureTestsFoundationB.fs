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
    check "arch: MessageTransformPipeline references ToolCatalog.all" (content.Contains "Wanxiangshu.Kernel.ToolCatalog.all")
    check "arch: MessageTransformPipeline does not hardcode tool catalog list" (not (content.Contains "let catalogNames =\n            [ \"coder\""))

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

let wanxiangzhenReconcile () =
    let replay = requireFile "src/Shell/Wanxiangzhen/CoordinatorReplay.fs" |> nonCommentCode
    check "arch: CoordinatorReplay has Submitted to Running fallback" (replay.Contains "Submitted" && replay.Contains "Running")
    check "arch: CoordinatorReplay does not touch Cancelled" (not (replay.Contains "withReconciledStatus t SquadTaskStatus.Merged" && replay.Contains "Cancelled"))