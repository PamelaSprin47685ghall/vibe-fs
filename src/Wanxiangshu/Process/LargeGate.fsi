namespace Wanxiangshu.Process

open System.Threading
open System.Threading.Tasks

module LargeGate =
    val getCount: unit -> int
    val acquire: ct: CancellationToken -> Task
    val release: unit -> unit
