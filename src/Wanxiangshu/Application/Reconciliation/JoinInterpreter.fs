namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain.JoinProgram
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Session

/// Production interpreter for JoinProgram (P0-RECOVERY-JOIN-001 / FLOW-003).
/// JoinAny → JoinWithPermit; JoinAvailable → JoinAvailableWithPermit.
/// JoinTool never bare-calls runtime.Join.
module JoinInterpreter =

    /// Single-result path (JoinAny). JoinAvailable in this tree is a programmer error.
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
                | JoinProgram.JoinAvailable _ ->
                    return failwith "JoinInterpreter.interpret: JoinAvailable requires interpretBatch"
            }

        go program

    /// EXEC-018 batch path (JoinAvailable). JoinAny in this tree is a programmer error.
    let interpretBatch
        (runtime: HostForkRuntime)
        (program: JoinProgram<Result<JoinWaitOutcome<RunCompletion>, ForkError>, 'result>)
        : Task<'result> =
        let rec go (current: JoinProgram<Result<JoinWaitOutcome<RunCompletion>, ForkError>, 'result>) : Task<'result> =
            task {
                match current with
                | JoinProgram.Return value -> return value
                | JoinProgram.JoinAvailable(permit, maxCount, interrupt, next) ->
                    let! outcome = runtime.JoinAvailableWithPermit(permit, maxCount, interrupt)
                    return! go (next outcome)
                | JoinProgram.JoinAny _ -> return failwith "JoinInterpreter.interpretBatch: JoinAny requires interpret"
            }

        go program

    /// Convenience: Domain joinAny program then interpret (single result).
    let runJoinAny (runtime: HostForkRuntime) (permit: FamilyRecoveryPermit) : Task<Result<RunCompletion, ForkError>> =
        interpret runtime (joinAny permit)

    /// Convenience: Domain joinAvailable program then interpretBatch.
    let runJoinAvailable
        (runtime: HostForkRuntime)
        (permit: FamilyRecoveryPermit)
        (maxCount: int)
        (interrupt: Task<unit>)
        : Task<Result<JoinWaitOutcome<RunCompletion>, ForkError>> =
        interpretBatch runtime (joinAvailable permit maxCount interrupt)
