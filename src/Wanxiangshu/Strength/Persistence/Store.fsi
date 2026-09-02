namespace Wanxiangshu.Strength.Persistence

open System
open System.Text
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength

[<RequireQualifiedAccess>]
module StrengthStore =
    val eventIdFor: sha256: (string -> string) -> decisionId: StrengthDecisionId -> eventType: string -> EventId

    val encodeFrameBundlePayload: bundle: StrengthFrameBundle -> byte[]

    val decodeFrameBundlePayload: sha256: (string -> string) -> content: byte[] -> Result<StrengthFrameBundle, string>

    val toEnvelope: sha256: (string -> string) -> event: StrengthEvent -> EventEnvelope

    val tryDecodeEnvelope: envelope: EventEnvelope -> Result<StrengthEvent, string>

    val loadFrameBundle:
        store: IEventStore ->
        sha256: (string -> string) ->
        prepared: StrengthCandidatePrepared ->
            Task<Result<StrengthFrameBundle, string>>

    val append:
        store: IEventStore -> sha256: (string -> string) -> event: StrengthEvent -> Task<Result<unit, AppendError>>

    val publishWithPayloads:
        store: IEventStore ->
        sha256: (string -> string) ->
        contents: byte[] list ->
        buildEvent: (PayloadRef list -> StrengthEvent) ->
            Task<Result<StrengthEvent, PublishError>>
