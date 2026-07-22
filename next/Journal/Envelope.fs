namespace Wanxiangshu.Next.Journal

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

type StreamId =
    | Workspace
    | Session of SessionId
    | Child of ChildId
    | Squad of SquadId
    | Process of ProcessId

type Envelope =
    { RuntimeId: RuntimeId
      LocalSeq: LocalSeq
      ObservedAt: ObservedAt
      EventId: EventId
      Stream: StreamId
      TurnId: TurnId option
      Fact: Fact }

module Envelope =

    let private extra = Extra.empty |> Extra.withInt64

    let compareSortKey (a: Envelope) (b: Envelope) : int =
        let cmpObs = a.ObservedAt.CompareTo(b.ObservedAt)

        if cmpObs <> 0 then
            cmpObs
        else
            let cmpRt =
                String.Compare(RuntimeId.value a.RuntimeId, RuntimeId.value b.RuntimeId, StringComparison.Ordinal)

            if cmpRt <> 0 then
                cmpRt
            else
                let seqA = LocalSeq.value a.LocalSeq
                let seqB = LocalSeq.value b.LocalSeq
                seqA.CompareTo(seqB)

    let serialize (env: Envelope) : string =
        Encode.Auto.toString (0, env, extra = extra)

    let deserialize (json: string) : Result<Envelope, string> =
        match Decode.Auto.fromString<Envelope> (json, extra = extra) with
        | Ok env -> Ok env
        | Error err -> Error err
