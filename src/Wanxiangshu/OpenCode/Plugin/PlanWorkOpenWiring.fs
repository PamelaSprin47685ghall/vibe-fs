namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.OpenCode.Host

// planning-020 entry wiring: lifecycle opening owns its separate wanxiangli
// journal. The legacy workspace EventStore is not an admission precondition.
module PlanWorkOpenWiring =

    let private runPlanWorkOpen
        (gitCommonDir: string)
        (root: string)
        (sid: string)
        (observedHead: int64 option)
        : Task<unit> =
        task {
            match!
                Wanxiangshu.Mission.Planning.PlanWorkOpen.ensurePlanWorkOpened gitCommonDir root sid observedHead
            with
            | Ok() -> ()
            | Error err ->
                Diagnostic.emit "plan-work-open-failed" [ "work_id", sid; "error", err ]
                invalidOp ("Plan lifecycle opening failed: " + err)
        }

    let ensurePlanWorkOpenedForSession
        (workspaceDirectory: string option)
        (sid: string)
        (observedHead: int64 option)
        : Task<unit> =
        task {
            match workspaceDirectory with
            | Some workspace when not (System.String.IsNullOrWhiteSpace workspace) ->
                let commonDir = Wanxiangshu.Persistence.Journal.RuntimePath.gitCommonDir workspace
                do! runPlanWorkOpen commonDir (commonDir + "/wanxiangshu") sid observedHead
            | _ ->
                Diagnostic.emit "plan-work-open-directory-unavailable" [ "work_id", sid ]
                invalidOp "Plan lifecycle opening failed: workspace directory is unavailable"
        }
