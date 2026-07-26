namespace Wanxiangshu.Next.Process

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module Spool =

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("tmpdir", "node:os")>]
    let private tmpdir () : string = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string) (data: byte[]) : unit = jsNative

    [<Import("appendFileSync", "node:fs")>]
    let private appendFileSync (path: string) (data: byte[]) : unit = jsNative

    [<Import("openSync", "node:fs")>]
    let private openSync (path: string) (flags: string) : int = jsNative

    [<Import("readSync", "node:fs")>]
    let private readSync (fd: int) (buffer: byte[]) (offset: int) (length: int) (position: int) : int = jsNative

    [<Import("closeSync", "node:fs")>]
    let private closeSync (fd: int) : unit = jsNative

    [<Import("createReadStream", "node:fs")>]
    let private createReadStream (path: string) (options: obj) : obj = jsNative

    [<Emit("""
        (async function(stream, consume) {
            for await (const input of stream) {
                const bytes = Buffer.from(input);
                for (let offset = 0; offset < bytes.length; offset += 204800) {
                    const end = Math.min(offset + 204800, bytes.length);
                    await consume(new Uint8Array(bytes.buffer, bytes.byteOffset + offset, end - offset));
                }
            }
        })($0, $1)
    """)>]
    let private consumeStreamAsync (stream: obj) (consume: byte[] -> Task<unit>) : Task<unit> = jsNative

    [<Emit("Math.random().toString(36).substring(2, 10)")>]
    let private randomString () : string = jsNative

    [<Literal>]
    let ChunkSizeBytes: int = 204800

    type StreamingSpool =
        { Path: string
          mutable BytesWritten: int64 }

    let private newTempPath () =
        pathJoin (tmpdir ()) (sprintf "spool-%s.tmp" (randomString ()))

    let chunkCount (bytes: int64) : int =
        if bytes <= 0L then
            0
        else
            int (((bytes - 1L) / int64 ChunkSizeBytes) + 1L)

    let startStreamingSpool () : StreamingSpool =
        let path = newTempPath ()
        writeFileSync path [||]
        { Path = path; BytesWritten = 0L }

    let appendStreamingSpool (spool: StreamingSpool) (bytes: byte[]) : unit =
        if not (isNull bytes) && bytes.Length > 0 then
            appendFileSync spool.Path bytes
            spool.BytesWritten <- spool.BytesWritten + int64 bytes.Length

    /// Calls the consumer once per at-most-200KB chunk without retaining prior chunks.
    let readChunksSync (path: string) (consume: byte[] -> unit) : unit =
        let fd = openSync path "r"
        let buffer = Array.zeroCreate<byte> ChunkSizeBytes
        let mutable position = 0
        let mutable doneReading = false

        try
            while not doneReading do
                let count = readSync fd buffer 0 ChunkSizeBytes position

                if count <= 0 then
                    doneReading <- true
                else
                    let chunk = Array.zeroCreate<byte> count
                    Array.blit buffer 0 chunk 0 count
                    consume chunk
                    position <- position + count
        finally
            closeSync fd

    /// Asynchronously maps each at-most-200KB chunk and releases it before reading the next one.
    let readChunks (path: string) (consume: byte[] -> Task<unit>) : Task<unit> =
        let options = createObj [ "highWaterMark" ==> ChunkSizeBytes ]
        createReadStream path options |> fun stream -> consumeStreamAsync stream consume

    let streamChunks (path: string) (consume: byte[] -> Task<unit>) : Task<unit> = readChunks path consume

    /// Pure helper retained for deterministic chunk-boundary tests and small caller-owned buffers.
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

    /// Writes bytes to a spool and returns metadata without allocating chunk arrays.
    let spoolBytesToTempFile (bytes: byte[]) : string * int64 * int =
        let spool = startStreamingSpool ()
        appendStreamingSpool spool bytes
        spool.Path, spool.BytesWritten, chunkCount spool.BytesWritten
