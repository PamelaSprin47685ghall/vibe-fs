namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Threading.Tasks

/// Per-PTY session aggregate. Replaces the dual live/pending maps with one
/// value that carries its wanxiangli/pty terminal, pending queue and exit signal.
/// DSL-state-combination: physical — PTY backend/output/lifecycle runtime state
type PtySession =
    { PtyId: string
      mutable Backend: obj
      mutable Closed: bool
      mutable ExitCompletion: TaskCompletionSource<unit>
      mutable ExitCompleted: bool
      mutable Pending: ResizeArray<PtyCommand * TaskCompletionSource<Result<unit, string>> option> }

module PtySession =
    let create (id: string) (backend: obj) : PtySession =
        { PtyId = id
          Backend = backend
          Closed = false
          ExitCompletion = TaskCompletionSource<unit>()
          ExitCompleted = false
          Pending = ResizeArray<_>() }
