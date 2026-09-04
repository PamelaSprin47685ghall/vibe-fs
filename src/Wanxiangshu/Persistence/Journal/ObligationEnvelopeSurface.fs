namespace Wanxiangshu.Persistence.Journal

open System
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

/// Pure canonical envelope/fold owner for obligation facts. Journal handles are
/// intentionally absent; durable append/read stays in ObligationJournalSurface.
[<RequireQualifiedAccess>]
module ObligationEnvelopeSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private streamOfSession (sessionId: string) =
        StreamId.Session(SessionId.create sessionId)

    let serializeMagicTodoEnvelope (typed: string) : string =
        match MagicTodoFactCodec.tryDecode typed with
        | Error error -> failwith ("ObligationEnvelopeSurface: invalid MagicTodo payload: " + error)
        | Ok fact -> FactCodec.serializeFact (Fact.MagicTodo fact)

    let deserializeMagicTodoEnvelope (encoded: string) : obj =
        match FactCodec.deserializeFact encoded with
        | Error error -> box {| ok = false; error = error |}
        | Ok(Fact.MagicTodo typed) ->
            box
                {| ok = true
                   case = "MagicTodo"
                   payload = MagicTodoFactCodec.encode typed |}
        | Ok _ ->
            box
                {| ok = false
                   error = "not a MagicTodo fact" |}

    let private envelopeForMagic (sessionId: string) (providerRun: string) (typed: string) : Envelope =
        match MagicTodoFactCodec.tryDecode typed with
        | Error error -> failwith ("ObligationEnvelopeSurface: invalid MagicTodo payload: " + error)
        | Ok fact ->
            { RuntimeId = RuntimeId.create "obligation-ledger-surface"
              LocalSeq = LocalSeq.create 1L
              ObservedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
              EventId = EventId.create "obligation-ledger-surface-event"
              Stream = streamOfSession sessionId
              ProviderRun = Some(ProviderRunIdentity.create providerRun)
              Fact = Fact.MagicTodo fact }

    let foldMagicEnvelope (sessionId: string) (providerRun: string) (typed: string) : obj =
        match Fold.foldEnvelope Fold.empty (envelopeForMagic sessionId providerRun typed) with
        | Error rejection ->
            box
                {| ok = false
                   error = rejection.Reason |}
        | Ok projection ->
            let magic = projection.AgentProjections.MagicTodo

            let incumbencies =
                magic.ByIncumbency
                |> Map.toArray
                |> Array.map (fun (incumbencyId, incumbency) ->
                    box
                        {| incumbencyId = incumbencyId
                           checkpoints = incumbency.Checkpoints.Count
                           proposedDigests =
                            incumbency.Checkpoints
                            |> Map.toArray
                            |> Array.map (fun (_, checkpoint) -> BlobDigest.value checkpoint.ProposedTodoDigest) |})

            box
                {| ok = true
                   incumbencies = incumbencies |}
