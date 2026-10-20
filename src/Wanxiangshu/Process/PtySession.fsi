namespace Wanxiangshu.Process

open System.Threading.Tasks

type PtySession =
    { PtyId: string
      mutable Backend: obj
      mutable Closed: bool
      mutable ExitCompletion: TaskCompletionSource<unit>
      mutable ExitCompleted: bool
      mutable Pending: ResizeArray<PtyCommand * TaskCompletionSource<Result<unit, string>> option> }

module PtySession =
    val create: id: string -> backend: obj -> PtySession
