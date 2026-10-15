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
                // Root is the worktree the session was opened in. The state
                // directory is only where the journal lives; writing it as root
                // makes the same transform's manager opening see a different
                // work and refuse the first prompt.
                let root =
                    Wanxiangshu.Git.GitSubject.execIn workspace [| "rev-parse"; "--show-toplevel" |]
                    |> fun path -> path.TrimEnd('\r', '\n')

                if System.String.IsNullOrWhiteSpace root then
                    Diagnostic.emit "plan-work-open-directory-unavailable" [ "work_id", sid ]
                    invalidOp "Plan lifecycle opening failed: Git did not identify a worktree root"

                let commonDir = Wanxiangshu.Persistence.Journal.RuntimePath.initializeStateRoot root
                do! runPlanWorkOpen commonDir root sid observedHead
            | _ ->
                Diagnostic.emit "plan-work-open-directory-unavailable" [ "work_id", sid ]
                invalidOp "Plan lifecycle opening failed: workspace directory is unavailable"
        }
