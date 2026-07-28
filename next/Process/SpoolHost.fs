namespace Wanxiangshu.Next.Process

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Host file I/O primitives used by the spool facade. All Node/Bun interop for
/// temporary spool files lives here.
module SpoolHost =

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

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

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

    let tempPath () : string =
        pathJoin (tmpdir ()) (sprintf "spool-%s.tmp" (Guid.NewGuid().ToString("N").Substring(0, 8)))

    let writeFile (path: string) (data: byte[]) : unit =
        if not (isNull data) then
            writeFileSync path data

    let appendFile (path: string) (data: byte[]) : unit =
        if not (isNull data) && data.Length > 0 then
            appendFileSync path data

    let deleteFile (path: string) : unit =
        try
            if not (String.IsNullOrWhiteSpace path) && existsSync path then
                unlinkSync path
        with _ ->
            ()

    let readFileSyncChunks (path: string) (chunkSize: int) (consume: byte[] -> unit) : unit =
        let fd = openSync path "r"
        let buffer = Array.zeroCreate<byte> chunkSize
        let mutable position = 0
        let mutable done' = false

        try
            while not done' do
                let count = readSync fd buffer 0 chunkSize position

                if count <= 0 then
                    done' <- true
                else
                    let chunk = Array.zeroCreate<byte> count
                    Array.blit buffer 0 chunk 0 count
                    consume chunk
                    position <- position + count
        finally
            closeSync fd

    let readFileAsyncChunks (path: string) (chunkSize: int) (consume: byte[] -> Task<unit>) : Task<unit> =
        let options = createObj [ "highWaterMark" ==> chunkSize ]
        createReadStream path options |> fun stream -> consumeStreamAsync stream consume
