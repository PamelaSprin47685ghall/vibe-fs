namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks

/// Per-PTY session aggregate. Replaces the dual live/pending maps with one
/// value that carries its own output buffer, pending queue and exit signal.
/// DSL-state-combination: physical — PTY backend/output/lifecycle runtime state
type PtySession =
    {
        PtyId: string
        mutable Backend: obj
        mutable OutputBuffer: StringBuilder
        mutable Closed: bool
        /// PTY-READ-FIRST-BYTE: a read arrived while the buffer was empty and the terminal open,
        /// so the next byte (or the quiet bound) owns the answer. Physical I/O state, not a
        /// workflow stage: exactly one read may be in flight per PTY.
        mutable AwaitingFirstByte: bool
        mutable ExitCompletion: TaskCompletionSource<unit>
        mutable ExitCompleted: bool
        mutable Pending: ResizeArray<PtyCommand * TaskCompletionSource<Result<unit, string>> option>
    }

module PtySession =
    let create (id: string) (backend: obj) : PtySession =
        { PtyId = id
          Backend = backend
          OutputBuffer = StringBuilder()
          Closed = false
          AwaitingFirstByte = false
          ExitCompletion = TaskCompletionSource<unit>()
          ExitCompleted = false
          Pending = ResizeArray<_>() }
