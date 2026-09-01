namespace Wanxiangshu.Persistence.Journal

open Thoth.Json
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

type StreamId =
    | Workspace
    | Session of SessionId
    | Child of ChildId
    | Process of ProcessId

type Envelope =
    { RuntimeId: RuntimeId
      LocalSeq: LocalSeq
      ObservedAt: ObservedAt
      EventId: EventId
      Stream: StreamId
      ProviderRun: ProviderRunIdentity option
      Fact: Fact }

module Envelope =
    val compareSortKey: a: Envelope -> b: Envelope -> int
    val serialize: envelope: Envelope -> string
    val deserialize: json: string -> Result<Envelope, string>
    val deserializeValue: value: JsonValue -> Result<Envelope, string>
