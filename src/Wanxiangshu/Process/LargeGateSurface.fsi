namespace Wanxiangshu.Process

open System.Threading.Tasks

module LargeGateSurface =
    val createToken: cancelled: bool -> obj
    val cancelToken: token: obj -> unit
    val isCancellationRequested: token: obj -> bool
    val acquire: token: obj -> Task
    val release: unit -> unit
    val getCount: unit -> int
    val runLargeEstimate: observe: (unit -> unit) -> Task<bool>
