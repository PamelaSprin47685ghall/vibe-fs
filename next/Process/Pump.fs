namespace Wanxiangshu.Next.Process

open System
open System.IO
open System.Threading
open System.Threading.Tasks

module Pump =

    /// Losslessly pumps bytes from a Stream into a byte array until EOF or cancellation.
    let pumpStreamAsync (stream: Stream) (ct: CancellationToken) : Task<byte[]> =
        task {
#if FABLE_COMPILER
            return [||]
#else
            if isNull stream then
                return [||]
            else
                use ms = new MemoryStream()
                let buffer = Array.zeroCreate<byte> 8192

                try
                    let mutable reading = true

                    while reading && not ct.IsCancellationRequested do
                        let! count = stream.ReadAsync(buffer, 0, buffer.Length, ct)

                        if count > 0 then
                            ms.Write(buffer, 0, count)
                        else
                            reading <- false
                with
                | :? OperationCanceledException -> ()
                | :? IOException -> ()
                | :? ObjectDisposedException -> ()

                return ms.ToArray()
#endif
        }
