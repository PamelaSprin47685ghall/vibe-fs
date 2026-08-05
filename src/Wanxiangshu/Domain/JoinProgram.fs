namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.SessionRecovery

/// P0-RECOVERY-JOIN-001 §五: closed join program. Data only — Interpreter executes.
/// `JoinAny` consumes a private-token FamilyRecoveryPermit; no bare join effect.
module JoinProgram =

    /// Free `'outcome` is materialised by the production interpreter (Session join Result).
    type JoinProgram<'outcome, 'result> =
        | Return of 'result
        | JoinAny of FamilyRecoveryPermit * ('outcome -> JoinProgram<'outcome, 'result>)

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

            bind program

    let join = JoinBuilder()

    /// Build a join program that must present FamilyRecoveryPermit to the interpreter.
    let joinAny (permit: FamilyRecoveryPermit) : JoinProgram<'outcome, 'outcome> = JoinAny(permit, Return)
