namespace Wanxiangshu.Next.Process

open System.IO
open System.Threading
open System.Threading.Tasks

#if FABLE_COMPILER
open Fable.Core
#endif

module Pump =

    /// Losslessly pumps bytes from a Stream into a byte array until EOF or cancellation.
#if FABLE_COMPILER
    [<Emit("""
        (async function(stream, ct) {
            const chunks = [];
            const cancelled = () => ct && (ct.IsCancellationRequested || ct.isCancellationRequested);
            if (stream && stream[Symbol.asyncIterator]) {
                for await (const chunk of stream) {
                    if (cancelled()) throw new Error('PROCESS_PUMP_CANCELLED');
                    chunks.push(Buffer.from(chunk));
                }
            } else {
                await new Promise((resolve, reject) => {
                    stream.on('data', chunk => {
                        if (cancelled()) { reject(new Error('PROCESS_PUMP_CANCELLED')); return; }
                        chunks.push(Buffer.from(chunk));
                    });
                    stream.on('end', resolve);
                    stream.on('error', reject);
                });
            }
            return new Uint8Array(Buffer.concat(chunks));
        })($0, $1)
    """)>]
    let private pumpJavaScript (stream: Stream) (ct: CancellationToken) : Task<byte[]> = jsNative

    let pumpStreamAsync (stream: Stream) (ct: CancellationToken) : Task<byte[]> =
        if isNull stream then
            nullArg (nameof stream)

        pumpJavaScript stream ct
#else
    let pumpStreamAsync (stream: Stream) (ct: CancellationToken) : Task<byte[]> =
        task {
            if isNull stream then
                nullArg (nameof stream)

            use buffer = new MemoryStream()
            do! stream.CopyToAsync(buffer, 81920, ct)
            return buffer.ToArray()
        }
#endif
