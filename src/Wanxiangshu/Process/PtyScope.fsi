namespace Wanxiangshu.Process

open Fable.Core

module internal PtyScope =
    val resolveWorkingDirectory: cwd: string -> string
    val load: unit -> JS.Promise<obj>
    val membershipRows: owner: int -> sampler: int -> text: string -> obj
