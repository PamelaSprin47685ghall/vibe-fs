namespace Wanxiangshu.Process

open System.Threading.Tasks

/// Host binding to `wanxiangli/spool`. The library owns memory bounds, the spool
/// file and its write failures; this module only adapts its messages to F#.
module Spool =
    [<Literal>]
    val ChunkSizeBytes: int = 204800

    /// One live `OutputSpool` instance from the library.
    type OutputSpool

    /// Library result of `finish()`, already decoded.
    [<RequireQualifiedAccess>]
    type Finished =
        | Memory of stdout: byte array * stderr: byte array
        | File of path: string * bytes: int64
        | Failed of observed: int64 * written: int64 * operation: string * code: string option * message: string

    val chunkCount: bytes: int64 -> int
    val create: memoryBytes: int -> OutputSpool
    val append: spool: OutputSpool -> channel: string -> bytes: byte array -> unit
    val observed: spool: OutputSpool -> int64
    val held: spool: OutputSpool -> int64
    val isFile: spool: OutputSpool -> bool
    val finish: spool: OutputSpool -> Finished
    val discard: spool: OutputSpool -> unit
    val readChunks: path: string -> consume: (byte array -> Task<unit>) -> Task<unit>
    val readAllBytes: path: string -> Task<byte array>
    val delete: path: string -> unit
