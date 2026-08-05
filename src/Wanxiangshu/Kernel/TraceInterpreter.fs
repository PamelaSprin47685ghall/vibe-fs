namespace Wanxiangshu.Kernel

/// FLOW-003: shared trace walker over the generic Program kernel.
/// Drives every Suspend continuation with null; collects instruction order only.
module TraceInterpreter =

    let rec trace (program: Program<'instruction, 'result>) : 'instruction list * 'result =
        match program with
        | Pure value -> [], value
        | Suspend(instruction, next) ->
            let rest, result = trace (next null)
            instruction :: rest, result
