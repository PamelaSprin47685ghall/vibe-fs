namespace Wanxiangshu.Foundation

open System.Threading
open System.Threading.Tasks

module Defer =
    val defer<'T>: work: (unit -> Task<'T>) -> Task<'T>

module Parallel =
    val mapBounded:
        maxConcurrency: int ->
        cancellation: CancellationToken ->
        action: ('t -> CancellationToken -> Task<'u>) ->
        items: 't seq ->
        Task<'u list>
