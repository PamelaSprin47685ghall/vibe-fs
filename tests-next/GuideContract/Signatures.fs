namespace Wanxiangshu.Next.Tests.GuideContract

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.OpenCode

module Signatures =

    let assertTypes () =
        ignore (typeof<Flow<unit, string, int>>)
        ignore (typeof<Envelope>)
        ignore (typeof<Deadline>)
        ignore (typeof<RuntimeSnapshot>)

        let _run: 'c -> CancellationToken -> Flow<'c, 'e, 'a> -> Task<Result<'a, 'e>> =
            Flow.run

        let _fail: 'e -> Flow<'c, 'e, 'a> = Flow.fail
        let _attempt: Flow<'c, 'e, 'a> -> Flow<'c, 'e, Result<'a, 'e>> = Flow.attempt

        let _mapBounded: int -> CancellationToken -> ('t -> CancellationToken -> Task<'u>) -> 't seq -> Task<'u list> =
            Parallel.mapBounded

        ignore (typeof<CommitResult<Envelope>>)
        ignore (typeof<BootSnapshot>)
        ignore (typeof<ProjectionSet>)
        ignore MessageTransform.transform
        ignore (typeof<MessageWatermark>)
        ignore MessageTransform.replacePrefix
        ignore FactCodec.serializeFact
        ignore Boot.kWayMerge

    [<Fact>]
    let Guide_types_exist () = assertTypes ()
