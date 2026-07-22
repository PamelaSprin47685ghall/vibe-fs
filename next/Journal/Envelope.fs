namespace Wanxiangshu.Next.Journal

open System
open System.Text.Json
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

    // Serialization DTOs for JSON NDJSON line
    type StreamDto = { Kind: string; Id: string option }

    type EnvelopeDto =
        { RuntimeId: string
          LocalSeq: int64
          ObservedAt: DateTimeOffset
          EventId: string
          Stream: StreamDto
          TurnId: string option
          FactJson: string }

    let private options = JsonSerializerOptions(WriteIndented = false)

    let private encodeStream (stream: StreamId) : StreamDto =
        match stream with
        | Workspace -> { Kind = "Workspace"; Id = None }
        | Session id ->
            { Kind = "Session"
              Id = Some(SessionId.value id) }
        | Child id ->
            { Kind = "Child"
              Id = Some(ChildId.value id) }
        | Squad id ->
            { Kind = "Squad"
              Id = Some(SquadId.value id) }
        | Process id ->
            { Kind = "Process"
              Id = Some(ProcessId.value id) }

    let private decodeStream (dto: StreamDto) : Result<StreamId, string> =
        match dto.Kind with
        | "Workspace" -> Ok Workspace
        | "Session" ->
            match dto.Id with
            | Some id -> Ok(Session(SessionId.create id))
            | None -> Error "Missing SessionId"
        | "Child" ->
            match dto.Id with
            | Some id -> Ok(Child(ChildId.create id))
            | None -> Error "Missing ChildId"
        | "Squad" ->
            match dto.Id with
            | Some id -> Ok(Squad(SquadId.create id))
            | None -> Error "Missing SquadId"
        | "Process" ->
            match dto.Id with
            | Some id -> Ok(Process(ProcessId.create id))
            | None -> Error "Missing ProcessId"
        | other -> Error $"Unknown StreamKind: {other}"

    let serialize (env: Envelope) : string =
        let dto: EnvelopeDto =
            { RuntimeId = RuntimeId.value env.RuntimeId
              LocalSeq = LocalSeq.value env.LocalSeq
              ObservedAt = env.ObservedAt
              EventId = EventId.value env.EventId
              Stream = encodeStream env.Stream
              TurnId = env.TurnId |> Option.map TurnId.value
              FactJson = FactCodec.serializeFact env.Fact }

        JsonSerializer.Serialize(dto, options)

    let deserialize (json: string) : Result<Envelope, string> =
        try
            if String.IsNullOrWhiteSpace(json) then
                Error "Empty JSON line"
            else
                let dto = JsonSerializer.Deserialize<EnvelopeDto>(json, options)

                match decodeStream dto.Stream with
                | Error err -> Error err
                | Ok stream ->
                    match FactCodec.deserializeFact dto.FactJson with
                    | Error err -> Error err
                    | Ok fact ->
                        let env: Envelope =
                            { RuntimeId = RuntimeId.create dto.RuntimeId
                              LocalSeq = LocalSeq.create dto.LocalSeq
                              ObservedAt = dto.ObservedAt
                              EventId = EventId.create dto.EventId
                              Stream = stream
                              TurnId = dto.TurnId |> Option.map TurnId.create
                              Fact = fact }

                        Ok env
        with ex ->
            Error ex.Message
