namespace Wanxiangshu.Next.Session

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

type ChildRequest = { Prompt: string }

type ChildResult =
    | CompletedChild of string
    | FailedChild of string

type ChildScript =
    { GetOrCreateSession: ChildRequest -> ChildFlow<ChildSession> }

and ChildSession =
    { SessionId: string
      Run: string -> ChildFlow<ChildResult>
      Close: unit -> ChildFlow<unit> }

and ChildError =
    | ChildNoProgress of string
    | ChildCancelled
    | ChildExecutionError of string

and ChildFlow<'a> = Flow<ChildScript, ChildError, 'a>

module ChildFlows =

    let child = FlowBuilder<ChildScript, ChildError>(None)

    let runChild (c: ChildScript) (request: ChildRequest) : ChildFlow<ChildResult> =
        child {
            let! s = c.GetOrCreateSession(request)
            let! res = s.Run(request.Prompt)
            return res
        }

    let runParallel
        (maxConcurrency: int)
        (createScript: unit -> ChildScript)
        (requests: ChildRequest list)
        : ChildFlow<ChildResult list> =
        Flow.create (fun _ ct ->
            task {
                let action (req: ChildRequest) (childCt: CancellationToken) : Task<ChildResult> =
                    task {
                        let script = createScript ()
                        let flow = runChild script req
                        let! res = Flow.run script childCt flow

                        match res with
                        | Ok childRes -> return childRes
                        | Error err -> return FailedChild(sprintf "%A" err)
                    }

                let! results = Parallel.mapBounded maxConcurrency ct action requests
                return Ok results
            })
