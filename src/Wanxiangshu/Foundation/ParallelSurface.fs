namespace Wanxiangshu.Foundation

open System
open System.Threading
open System.Threading.Tasks

[<RequireQualifiedAccess>]
module ParallelSurface =
    type private TokenHandle(source: CancellationTokenSource) =
        member _.Source = source
        member _.Token = source.Token

    let liveToken () : obj =
        box (TokenHandle(new CancellationTokenSource()))

    let cancelledToken () : obj =
        let source = new CancellationTokenSource()
        source.Cancel()
        box (TokenHandle(source))

    let cancel (token: obj) : unit =
        (token :?> TokenHandle).Source.Cancel()

    let mapBounded
        (maxConcurrency: int)
        (action: obj)
        (items: obj array)
        (token: obj)
        : Task<obj array> =
        task {
            let handle =
                if isNull token then
                    TokenHandle(new CancellationTokenSource())
                else
                    token :?> TokenHandle

            let run item _ =
                let fn = unbox<obj -> obj -> Task<obj>> action
                fn item (box handle)

            let! results = Parallel.mapBounded maxConcurrency handle.Token run (items |> Array.toList)
            return results |> List.toArray
        }
