namespace Wanxiangshu.Process

open System
open System.Threading.Tasks

/// Pure spool facade. File I/O is delegated to NodeProcessHost so this file
/// contains no JS interop.
module Spool =

    [<Literal>]
    let ChunkSizeBytes: int = 204800

    /// DSL-state-combination: physical — BytesWritten is a byte-buffer spool counter
    type StreamingSpool =
        { Path: string
          mutable BytesWritten: int64 }

    let chunkCount (bytes: int64) : int =
        if bytes <= 0L then
            0
        else
            int (((bytes - 1L) / int64 ChunkSizeBytes) + 1L)

    let startStreamingSpool () : StreamingSpool =
        let path = NodeProcessHost.tempPath ()
        NodeProcessHost.writeFile path [||]
        { Path = path; BytesWritten = 0L }

    let appendStreamingSpool (spool: StreamingSpool) (bytes: byte[]) : unit =
        if not (isNull bytes) && bytes.Length > 0 then
            NodeProcessHost.appendFile spool.Path bytes
            spool.BytesWritten <- spool.BytesWritten + int64 bytes.Length

    let readChunksSync (path: string) (consume: byte[] -> unit) : unit =
        NodeProcessHost.readFileSyncChunks path ChunkSizeBytes consume

    let readChunks (path: string) (consume: byte[] -> Task<unit>) : Task<unit> =
        NodeProcessHost.readFileAsyncChunks path ChunkSizeBytes consume

    let chunkBytes (chunkSize: int) (bytes: byte[]) : byte[][] =
        if isNull bytes || bytes.Length = 0 then
            [||]
        else
            let total = bytes.Length
            let count = (total + chunkSize - 1) / chunkSize

            Array.init count (fun i ->
                let offset = i * chunkSize
                let len = Math.Min(chunkSize, total - offset)
                let chunk = Array.zeroCreate<byte> len
                Array.blit bytes offset chunk 0 len
                chunk)

    let spoolBytesToTempFile (bytes: byte[]) : string * int64 * int =
        let spool = startStreamingSpool ()
        appendStreamingSpool spool bytes
        spool.Path, spool.BytesWritten, chunkCount spool.BytesWritten

    let delete (path: string) : unit = NodeProcessHost.deleteFile path
