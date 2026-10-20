namespace Wanxiangshu.Process

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Host binding to `wanxiangli/spool`. The library owns memory bounds, the spool
/// file and its write failures; this module only adapts its messages to F#.
module Spool =

    [<Literal>]
    let ChunkSizeBytes: int = 204800

    [<Import("OutputSpool", "wanxiangli/spool")>]
    let private OutputSpoolCtor: obj = jsNative

    [<Import("tmpdir", "node:os")>]
    let private tmpdir () : string = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Emit("$0.readChunks($1, $2, $3)")>]
    let private readChunksJs
        (_ctor: obj)
        (_path: string)
        (_size: int)
        (_consume: byte array -> Task<unit>)
        : Task<obj> =
        jsNative

    [<Emit("new $0($1)")>]
    let private construct (_ctor: obj) (_options: obj) : obj = jsNative

    [<Emit("((chunks) => { let size = 0; for (const c of chunks) size += c.length; const out = new Uint8Array(size); let at = 0; for (const c of chunks) { out.set(c, at); at += c.length; } return out; })($0)")>]
    let private concat (_parts: byte array array) : byte array = jsNative

    /// DSL-class: PhysicalHandle — one library OutputSpool; owner wanxiangli/spool, law process-execution-009, proof process-execution-009.
    [<Sealed>]
    type OutputSpool(instance: obj) =
        member _.Instance = instance

    [<RequireQualifiedAccess>]
    type Finished =
        | Memory of stdout: byte array * stderr: byte array
        | File of path: string * bytes: int64
        | Failed of observed: int64 * written: int64 * operation: string * code: string option * message: string

    let chunkCount (bytes: int64) : int =
        if bytes <= 0L then
            0
        else
            int (((bytes - 1L) / int64 ChunkSizeBytes) + 1L)

    let create (memoryBytes: int) : OutputSpool =
        OutputSpool(
            construct
                OutputSpoolCtor
                (createObj [ "directory" ==> tmpdir (); "memoryBytes" ==> float (max 0 memoryBytes) ])
        )

    let append (spool: OutputSpool) (channel: string) (bytes: byte array) : unit =
        if not (isNull bytes) && bytes.Length > 0 then
            spool.Instance?append(channel, bytes) |> ignore

    let private state (spool: OutputSpool) : obj = spool.Instance?inspect()

    let observed (spool: OutputSpool) : int64 =
        int64 (unbox<float> (state spool)?observed)

    let held (spool: OutputSpool) : int64 = int64 (unbox<float> (state spool)?held)

    let isFile (spool: OutputSpool) : bool =
        not (isNull (unbox<obj> (state spool)?path))

    let private text (value: obj) =
        if isNullOrUndefined value then None else Some(string value)

    let finish (spool: OutputSpool) : Finished =
        let result: obj = spool.Instance?finish()

        match unbox<string> result?kind with
        | "Memory" -> Finished.Memory(unbox result?stdout, unbox result?stderr)
        | "File" -> Finished.File(unbox result?path, int64 (unbox<float> result?bytes))
        | _ ->
            let error: obj = result?error

            Finished.Failed(
                int64 (unbox<float> result?observed),
                int64 (unbox<float> result?written),
                string error?operation,
                text error?code,
                string error?message
            )

    let discard (spool: OutputSpool) : unit = spool.Instance?discard() |> ignore

    let readChunks (path: string) (consume: byte array -> Task<unit>) : Task<unit> =
        task {
            let! result = readChunksJs OutputSpoolCtor path ChunkSizeBytes consume

            if unbox<string> result?kind <> "Read" then
                let error: obj = result?error
                return raise (InvalidOperationException(sprintf "spool read failed: %s" (string error?message)))
        }

    let readAllBytes (path: string) : Task<byte array> =
        task {
            // DSL-MUTABLE: algorithm-scratch — chunk accumulator for one complete read
            let parts = ResizeArray<byte array>()

            do!
                readChunks path (fun bytes ->
                    parts.Add bytes
                    Task.FromResult(()))

            return concat (parts.ToArray())
        }

    let private unlinkIfNamed (path: string) =
        if not (String.IsNullOrWhiteSpace path) then
            unlinkSync path

    /// Removes a finished spool file whose path the caller received in an outcome.
    let delete (path: string) : unit =
        try
            unlinkIfNamed path
        with _ ->
            ()
