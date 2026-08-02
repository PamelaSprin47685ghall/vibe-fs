namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks

/// Per-PTY session aggregate. Replaces the dual live/pending maps with one
/// value that carries its own output buffer, pending queue and exit signal.
type PtySession =
    { PtyId: string
      mutable Backend: obj
      mutable OutputBuffer: StringBuilder
      mutable Closed: bool
      mutable ExitCompletion: TaskCompletionSource<unit>
      mutable Pending: ResizeArray<PtyCommand * TaskCompletionSource<Result<unit, string>> option> }

module PtySession =
    let create (id: string) (backend: obj) : PtySession =
        { PtyId = id
          Backend = backend
          OutputBuffer = StringBuilder()
          Closed = false
          ExitCompletion = TaskCompletionSource<unit>()
          Pending = ResizeArray<_>() }
