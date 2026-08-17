namespace Wanxiangshu.Persistence.Journal

open System
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
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
        FactCodec.serializeFact (Fact.MagicTodo typed)

    let deserializeMagicTodoEnvelope (encoded: string) : obj =
        match FactCodec.deserializeFact encoded with
        | Error error -> box {| ok = false; error = error |}
        | Ok(Fact.MagicTodo typed) ->
            box
                {| ok = true
                   case = "MagicTodo"
                   payload = typed |}
        | Ok _ ->
            box
                {| ok = false
                   error = "not a MagicTodo fact" |}

    let private envelopeForMagic (sessionId: string) (providerRun: string) (typed: string) : Envelope =
        { RuntimeId = RuntimeId.create "obligation-ledger-surface"
          LocalSeq = LocalSeq.create 1L
          ObservedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
          EventId = EventId.create "obligation-ledger-surface-event"
          Stream = streamOfSession sessionId
          ProviderRun = Some(ProviderRunIdentity.create providerRun)
          Fact = Fact.MagicTodo typed }

    let foldMagicEnvelope (sessionId: string) (providerRun: string) (typed: string) : obj =
        match Fold.foldEnvelope Fold.empty (envelopeForMagic sessionId providerRun typed) with
        | Error rejection ->
            box
                {| ok = false
                   error = rejection.Reason |}
        | Ok projection ->
            let magic = projection.AgentProjections.MagicTodo

            let lives =
                magic.ByLife
                |> Map.toArray
                |> Array.map (fun (lifeId, life) ->
                    box
                        {| lifeId = lifeId
                           checkpoints = life.Checkpoints.Count
                           proposedDigests =
                            life.Checkpoints
                            |> Map.toArray
                            |> Array.map (fun (_, checkpoint) -> BlobDigest.value checkpoint.ProposedTodoDigest) |})

            box {| ok = true; lives = lives |}

    let private managerLifecycleFactOf (caseName: string) (payload: obj) : ManagerLifecycleFact =
        let sessionId = SessionId.create (text (payload?sessionId))
        let lifeId = ManagerLifeId.create (text (payload?lifeId))

        match caseName with
        | "LifeOpened" ->
            ManagerLifecycleFact.LifeOpened
                {| SessionId = sessionId
                   LifeId = lifeId
                   OpeningUserMessageId = PhysicalUserMessageId.create (text (payload?openingUserMessageId))
                   OpeningTextRef = BlobRef.create (text (payload?openingTextRef))
                   OpeningTextDigest = BlobDigest.create (text (payload?openingTextDigest))
                   OpeningCursorSequence = int64 (unbox<int> (payload?openingCursorSequence)) |}
        | "WorkActivated" ->
            ManagerLifecycleFact.WorkActivated
                {| SessionId = sessionId
                   LifeId = lifeId
                   ActivationPromptKey = PromptKey.create (text (payload?activationPromptKey))
                   ProtectedPrefixEndSequence = int64 (unbox<int> (payload?protectedPrefixEndSequence)) |}
        | other -> failwith $"ObligationEnvelopeSurface: unknown lifecycle case '{other}'"

    let foldLifecycleSequence (sessionId: string) (events: obj array) : obj =
        let values = if isNull events then [||] else events
        // DSL-MUTABLE: algorithm-scratch — lifecycle fold accumulator
        let mutable current = Fold.empty
        // DSL-MUTABLE: algorithm-scratch — first fold rejection
        let mutable failure: string option = None
        // DSL-MUTABLE: algorithm-scratch — synthetic envelope sequence
        let mutable sequence = 0

        for value in values do
            if failure.IsNone then
                let caseName = text (value?caseName)
                let fact = managerLifecycleFactOf caseName (unbox<obj> (value?payload))
                sequence <- sequence + 1

                let envelope =
                    { RuntimeId = RuntimeId.create "obligation-ledger-lifecycle-surface"
                      LocalSeq = LocalSeq.create (int64 sequence)
                      ObservedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
                      EventId = EventId.create ($"obligation-ledger-lifecycle-event-{sequence}")
                      Stream = streamOfSession sessionId
                      ProviderRun = None
                      Fact = Fact.ManagerLifecycle fact }

                match Fold.foldEnvelope current envelope with
                | Error rejection -> failure <- Some rejection.Reason
                | Ok next -> current <- next

        match failure with
        | Some error -> box {| ok = false; error = error |}
        | None ->
            match AgentProjection.tryFind (SessionId.create sessionId) current.AgentProjections with
            | None ->
                box
                    {| ok = true
                       protectedPrefixEnd = null |}
            | Some session ->
                let protectedPrefixEnd =
                    session.ManagerLife
                    |> Option.bind (fun value -> value.CurrentLife)
                    |> Option.bind (fun value -> value.ProtectedPrefixEnd)
                    |> Option.map (fun value -> box (int value.Sequence))
                    |> Option.toObj

                box
                    {| ok = true
                       protectedPrefixEnd = protectedPrefixEnd |}
