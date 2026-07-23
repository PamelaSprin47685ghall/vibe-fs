namespace Wanxiangshu.Next.Process

open System
open System.IO

module Spool =

#if FABLE_COMPILER
    open Fable.Core
    open Fable.Core.JsInterop

    [<Import("join", "node:path")>]
    let private pathJoin (a: string) (b: string) : string = jsNative

    [<Import("tmpdir", "node:os")>]
    let private tmpdir () : string = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string) (data: byte[]) : unit = jsNative

    [<Import("writeFile", "node:fs/promises")>]
    let private writeFileAsync (path: string) (data: byte[]) : JS.Promise<unit> = jsNative

    [<Global("Buffer")>]
    let private nodeBuffer: obj = jsNative

    [<Emit("Math.random().toString(36).substring(2, 10)")>]
    let private randomString () : string = jsNative
#endif

    [<Literal>]
    let ChunkSizeBytes: int = 204800 // Exactly 200 * 1024 bytes (200KB)

    /// Pure helper to split a byte array into exactly 200KB chunks.
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

    /// Spools complete bytes to a temp file and splits them into 200KB chunks.
    let spoolBytesToTempFile (bytes: byte[]) : string * byte[][] =
#if FABLE_COMPILER
        let tempPath = pathJoin (tmpdir ()) (sprintf "spool-%s.tmp" (randomString ()))
        writeFileSync tempPath bytes
#else
        let tempPath = Path.GetTempFileName()
        File.WriteAllBytes(tempPath, bytes)
#endif
        let chunks = chunkBytes ChunkSizeBytes bytes
        (tempPath, chunks)

#if FABLE_COMPILER
    let spoolBytesToTempFileAsync (bytes: byte[]) : JS.Promise<string * byte[][]> =
        JS.Constructors.Promise.Create(fun resolve reject ->
            let tempPath = pathJoin (tmpdir ()) (sprintf "spool-%s.tmp" (randomString ()))
            let buf = emitJsExpr bytes "Buffer.from($0)"
            let p = writeFileAsync tempPath (unbox<byte[]> buf)

            emitJsExpr
                (p,
                 (fun () ->
                     let chunks = chunkBytes ChunkSizeBytes bytes
                     resolve (tempPath, chunks)),
                 (fun err -> reject err))
                "$0.then($1, $2)"
            |> ignore)
#endif
