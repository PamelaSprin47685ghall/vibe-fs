namespace Wanxiangshu.Domain

open System.Threading.Tasks
open Wanxiangshu.Domain.SessionRecovery

/// P0-RECOVERY-JOIN-001 §五: closed join program. Data only — Interpreter executes.
/// `JoinAny` / `JoinAvailable` consume FamilyRecoveryPermit; no bare join effect.
module JoinProgram =

    /// Free `'outcome` is materialised by the production interpreter.
    /// JoinAny → single Result; JoinAvailable → batch JoinWaitOutcome Result.
    type JoinProgram<'outcome, 'result> =
        | Return of 'result
        | JoinAny of FamilyRecoveryPermit * ('outcome -> JoinProgram<'outcome, 'result>)
        | JoinAvailable of
            FamilyRecoveryPermit *
            maxCount: int *
            interrupt: Task<unit> *
            ('outcome -> JoinProgram<'outcome, 'result>)

    type JoinBuilder() =
        member _.Return(value: 'result) : JoinProgram<'outcome, 'result> = Return value
        member _.ReturnFrom(program: JoinProgram<'outcome, 'result>) = program
        member _.Zero() : JoinProgram<'outcome, unit> = Return()

        member _.Delay(f: unit -> JoinProgram<'outcome, 'result>) : JoinProgram<'outcome, 'result> = f ()

        member _.Bind
            (program: JoinProgram<'outcome, 'a>, cont: 'a -> JoinProgram<'outcome, 'b>)
            : JoinProgram<'outcome, 'b> =
            let rec bind current =
                match current with
                | Return value -> cont value
                | JoinAny(permit, next) -> JoinAny(permit, (fun outcome -> bind (next outcome)))
                | JoinAvailable(permit, maxCount, interrupt, next) ->
                    JoinAvailable(permit, maxCount, interrupt, (fun outcome -> bind (next outcome)))

            bind program

    let join = JoinBuilder()

    /// Single-result join program (FamilyRecoveryPermit required).
    let joinAny (permit: FamilyRecoveryPermit) : JoinProgram<'outcome, 'outcome> = JoinAny(permit, Return)

    /// EXEC-018 batch join program: maxCount + local interrupt (≠ runtime.Cancel).
    let joinAvailable
        (permit: FamilyRecoveryPermit)
        (maxCount: int)
        (interrupt: Task<unit>)
        : JoinProgram<'outcome, 'outcome> =
        JoinAvailable(permit, maxCount, interrupt, Return)
