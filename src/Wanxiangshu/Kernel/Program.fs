namespace Wanxiangshu.Kernel

/// FLOW-002 / FLOW-007: shared minimal Program mechanism.
/// Closed instruction AST (data only). Interpreters execute; this type does not.
type Program<'instruction, 'result> =
    | Pure of 'result
    | Suspend of 'instruction * (obj -> Program<'instruction, 'result>)

module Program =

    let ``pure`` (value: 'result) : Program<'instruction, 'result> = Pure value

    let suspend
        (instruction: 'instruction)
        (next: 'reply -> Program<'instruction, 'result>)
        : Program<'instruction, 'result> =
        Suspend(instruction, (fun (reply: obj) -> next (unbox reply)))

    let rec bind
        (program: Program<'instruction, 'a>)
        (cont: 'a -> Program<'instruction, 'b>)
        : Program<'instruction, 'b> =
        match program with
        | Pure value -> cont value
        | Suspend(instruction, next) -> Suspend(instruction, (fun reply -> bind (next reply) cont))

    let map (program: Program<'instruction, 'a>) (f: 'a -> 'b) : Program<'instruction, 'b> =
        bind program (fun value -> Pure(f value))

    type ProgramBuilder() =
        member _.Return(value: 'result) : Program<'instruction, 'result> = Pure value

        member _.ReturnFrom(program: Program<'instruction, 'result>) : Program<'instruction, 'result> = program

        member _.Zero() : Program<'instruction, unit> = Pure()

        member _.Delay(f: unit -> Program<'instruction, 'result>) : Program<'instruction, 'result> = f ()

        member _.Bind
            (program: Program<'instruction, 'a>, cont: 'a -> Program<'instruction, 'b>)
            : Program<'instruction, 'b> =
            bind program cont

        member _.Combine
            (left: Program<'instruction, unit>, right: Program<'instruction, 'result>)
            : Program<'instruction, 'result> =
            bind left (fun () -> right)

    let program = ProgramBuilder()
