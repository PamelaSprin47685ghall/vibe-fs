namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain.JoinProgram
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Session

/// Production interpreter for JoinProgram (P0-RECOVERY-JOIN-001 / FLOW-003).
/// JoinAny → HostForkRuntime.JoinWithPermit only; JoinTool never calls runtime.Join.
module JoinInterpreter =

    /// Interpret a join program against one HostForkRuntime.
    let interpret
        (runtime: HostForkRuntime)
        (program: JoinProgram<Result<RunCompletion, ForkError>, 'result>)
        : Task<'result> =
        let rec go (current: JoinProgram<Result<RunCompletion, ForkError>, 'result>) : Task<'result> =
            task {
                match current with
                | JoinProgram.Return value -> return value
                | JoinProgram.JoinAny(permit, next) ->
                    let! outcome = runtime.JoinWithPermit(permit)
                    return! go (next outcome)
            }

        go program

    /// Convenience: Domain joinAny program then interpret.
    let runJoinAny (runtime: HostForkRuntime) (permit: FamilyRecoveryPermit) : Task<Result<RunCompletion, ForkError>> =
        interpret runtime (joinAny permit)
