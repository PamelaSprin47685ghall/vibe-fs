namespace Wanxiangshu.Process

open System.Threading.Tasks

module Spool =
    [<Literal>]
    val ChunkSizeBytes: int = 204800

    type StreamingSpool =
        { Path: string
          mutable BytesWritten: int64 }

    val chunkCount: bytes: int64 -> int
    val startStreamingSpool: unit -> StreamingSpool
    val appendStreamingSpool: spool: StreamingSpool -> bytes: byte array -> unit
    val readChunksSync: path: string -> consume: (byte array -> unit) -> unit
    val readChunks: path: string -> consume: (byte array -> Task<unit>) -> Task<unit>
    val chunkBytes: chunkSize: int -> bytes: byte array -> byte array array
    val spoolBytesToTempFile: bytes: byte array -> string * int64 * int
    val delete: path: string -> unit
