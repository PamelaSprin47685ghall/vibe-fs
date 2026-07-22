namespace Wanxiangshu.Next.Process

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

module ProcessPump =

    let private readBuffer (reader: TextReader) (buffer: char[]) (cancellation: CancellationToken) =
        task {
            try
                let! count = reader.ReadAsync(buffer.AsMemory(), cancellation).AsTask()
                return Ok count
            with
            | :? OperationCanceledException
            | :? ObjectDisposedException -> return Ok 0
            | ex -> return Error ex
        }

    let private appendChars (sb: StringBuilder) (buffer: char[]) (count: int) (maxChars: int) (truncated: bool) =
        if truncated then
            true
        elif sb.Length + count > maxChars then
            let allowed = Math.Max(0, maxChars - sb.Length)

            if allowed > 0 then
                sb.Append(buffer, 0, allowed) |> ignore

            true
        else
            sb.Append(buffer, 0, count) |> ignore
            false

    let pumpReader (reader: TextReader) (cancellation: CancellationToken) (maxChars: int) =
        task {
            let sb = StringBuilder()
            let buffer = Array.zeroCreate<char> 4096
            let mutable truncated, reading = false, true
            let mutable pumpEx: exn option = None

            while reading && not cancellation.IsCancellationRequested do
                let! readRes = readBuffer reader buffer cancellation

                match readRes with
                | Error ex ->
                    pumpEx <- Some ex
                    reading <- false
                | Ok 0 -> reading <- false
                | Ok count -> truncated <- appendChars sb buffer count maxChars truncated

            match pumpEx with
            | Some ex -> return raise ex
            | None -> return (sb.ToString(), truncated)
        }

    let configureStartInfo (cmd: Command) (ctx: ProcessContext option) =
        let psi =
            System.Diagnostics.ProcessStartInfo(
                FileName = cmd.FileName,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            )

        for arg in cmd.Arguments do
            psi.ArgumentList.Add(arg)

        (cmd.WorkingDirectory
         |> Option.orElse (ctx |> Option.bind (fun c -> c.WorkingDirectory)))
        |> Option.iter (fun w -> psi.WorkingDirectory <- w)

        cmd.Environment
        |> Option.iter (fun env ->
            for KeyValue(k, v) in env do
                psi.Environment.[k] <- v)

        psi

    let writeStdinAsync
        (proc: System.Diagnostics.Process)
        (stdinOpt: string option)
        (ct: CancellationToken)
        (dlOpt: Deadline option)
        : Task<Result<unit, string>> =
        let writeWithToken (token: CancellationToken) =
            task {
                try
                    match stdinOpt with
                    | None ->
                        do! proc.StandardInput.DisposeAsync().AsTask()
                        return Ok()
                    | Some input ->
                        let bytes = Encoding.UTF8.GetBytes(input)
                        do! proc.StandardInput.BaseStream.WriteAsync(ReadOnlyMemory(bytes), token).AsTask()
                        do! proc.StandardInput.BaseStream.FlushAsync(token)
                        do! proc.StandardInput.DisposeAsync().AsTask()
                        return Ok()
                with
                | :? OperationCanceledException ->
                    if ct.IsCancellationRequested then
                        return Error "Writing stdin cancelled"
                    elif dlOpt |> Option.exists (Deadline.isExpired (fun () -> DateTimeOffset.UtcNow)) then
                        return Error "Writing stdin timed out"
                    else
                        return Error "Writing stdin cancelled"
                | ex -> return Error(sprintf "Failed writing stdin: %s" ex.Message)
            }

        match dlOpt with
        | None -> writeWithToken ct
        | Some dl ->
            task {
                let rem = Deadline.remaining (fun () -> DateTimeOffset.UtcNow) dl
                use deadlineCts = new CancellationTokenSource(rem)

                use linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(ct, deadlineCts.Token)

                return! writeWithToken linkedCts.Token
            }
