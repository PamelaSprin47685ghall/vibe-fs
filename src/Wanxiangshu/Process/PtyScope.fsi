namespace Wanxiangshu.Process

open Fable.Core

module internal PtyScope =
    val load: unit -> JS.Promise<obj>
    val membershipRows: owner: int -> sampler: int -> text: string -> obj
