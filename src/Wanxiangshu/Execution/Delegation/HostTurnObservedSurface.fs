namespace Wanxiangshu.Execution.Delegation

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Persistence.Journal

/// Plain-data owner for the HostTurnObserved durable fact.
/// Typed execution facts and the Agent projection remain behind this boundary.
[<RequireQualifiedAccess>]
module HostTurnObservedSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private providerRunOf (value: obj) : ProviderRunIdentity option =
        if isNull value then None else Some(ProviderRunIdentity.create (text value))

    let private factOfJs (value: obj) : Fact =
        let sessionId = SessionId.create (text (value?SessionId))
        let providerRun = providerRunOf (value?ProviderRun)
        let observedAt = DateTimeOffset.Parse(text (value?ObservedAt))

        Fact.Agent(
            AgentFact.Execution(
                ExecutionFactCases.HostTurnObserved
                    {| SessionId = sessionId
                       ProviderRun = providerRun
                       ObservedAt = observedAt |}
            )
        )

    let private envelopeOfJs (value: obj) : Envelope =
        let session = SessionId.create (text (value?SessionId))

        { RuntimeId = RuntimeId.create "host-turn-surface"
          LocalSeq = LocalSeq.create 1L
          ObservedAt = DateTimeOffset.Parse(text (value?ObservedAt))
          EventId = EventId.create "host-turn-observed-surface"
          Stream = StreamId.Session session
          ProviderRun = providerRunOf (value?ProviderRun)
          Fact = factOfJs value }

    let private factToJs (fact: Fact) : obj =
        match fact with
        | Fact.Agent(AgentFact.Execution(ExecutionFactCases.HostTurnObserved payload)) ->
            box
                {| ok = true
                   case = "HostTurnObserved"
                   sessionId = SessionId.value payload.SessionId
                   providerRun =
                       match payload.ProviderRun with
                       | None -> null
                       | Some run -> box (ProviderRunIdentity.value run)
                   observedAt = payload.ObservedAt.ToOffset(TimeSpan.Zero).ToString("O")
                   line = FactCodec.serializeFact fact |}
        | _ -> box {| ok = false; error = "decoded fact is not HostTurnObserved" |}

    /// Encode one plain HostTurnObserved payload to canonical fact bytes.
    let serialize (value: obj) : string = factOfJs value |> FactCodec.serializeFact

    /// Decode one fact line without exposing the Fact DU.
    let deserialize (line: string) : obj =
        match FactCodec.deserializeFact line with
        | Ok fact -> factToJs fact
        | Error error -> box {| ok = false; error = error |}

    let private rejectionToJs (rejection: Wanxiangshu.Composition.Durable.FoldRejection) : obj =
        box {| Fact = rejection.Fact; Reason = rejection.Reason |}

    /// Fold the observation through the canonical Agent reducer and expose only
    /// whether it created a session projection. HostTurnObserved is an inbox
    /// observation; it must not mutate LinkageProjection by itself.
    let foldNoop (value: obj) : obj =
        match Wanxiangshu.Composition.Durable.Fold.foldEnvelope
                  Wanxiangshu.Composition.Durable.Fold.empty
                  (envelopeOfJs value) with
        | Ok projection ->
            let hasSession =
                AgentProjection.tryFind (SessionId.create (text (value?SessionId))) projection.AgentProjections
                |> Option.isSome

            box {| ok = true; hasSession = hasSession |}
        | Error rejection -> box {| ok = false; error = rejectionToJs rejection |}

    /// Stable dedupe identity supplied by the observation payload.
    let identityKey (value: obj) : string =
        let run = value?ProviderRun
        sprintf "%s|%s" (text (value?SessionId)) (if isNull run then "" else text run)
