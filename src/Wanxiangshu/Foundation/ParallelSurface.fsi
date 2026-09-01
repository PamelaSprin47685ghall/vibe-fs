namespace Wanxiangshu.Foundation

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module ParallelSurface =
    val liveToken: unit -> obj
    val cancelledToken: unit -> obj
    val cancel: token: obj -> unit

    val mapBounded: maxConcurrency: int -> action: obj -> items: obj array -> token: obj -> Task<obj array>
