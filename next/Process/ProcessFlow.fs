namespace Wanxiangshu.Next.Process

open System
open Wanxiangshu.Next.Kernel

module ProcessFlows =

    type ProcessFlow<'a> = Flow<ProcessContext, ProcessError, 'a>

    let execute (cmd: Command) : ProcessFlow<Fact.ProcessResult> =
        Flow.create (fun ctx ct ->
            task {
                let effectiveCmd =
                    match cmd.Deadline, ctx.DefaultTimeout with
                    | Some _, _ -> cmd
                    | None, Some budget ->
                        { cmd with
                            Deadline = Some(Deadline.ofBudget DateTimeOffset.UtcNow budget) }
                    | None, None -> cmd

                let! spawnResult = ProcessSpawn.spawn effectiveCmd (Some ctx) ct

                match spawnResult with
                | Error e -> return Error e
                | Ok handle ->
                    use h = handle
                    return! Flow.run ctx ct (h.RunToCompletion())
            })
