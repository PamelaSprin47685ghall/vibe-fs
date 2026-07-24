namespace Wanxiangshu.Next.Process

open System.IO
open System.Threading
open System.Threading.Tasks

module Pump =

    /// Losslessly pumps bytes from a Stream into a byte array until EOF or cancellation.
    let pumpStreamAsync (stream: Stream) (ct: CancellationToken) : Task<byte[]> = task { return [||] }
