namespace Wanxiangshu.Sphinx.V2.Runtime

open System
open System.Threading.Tasks
open Wanxiangshu.Sphinx.V2.Core

type AppendReceipt =
    { Cuts: string list
      AcceptedRevision: Revision }

type AppendFault =
    | Rejected of reason: string
    | Conflict of currentRevision: Revision

type Envelope = { EventId: string; Payload: string }

type IEventStorePort =
    abstract Append: inquiryId: InquiryId * batch: TransitionBatch -> Task<Result<AppendReceipt, AppendFault>>
    abstract ReadAll: inquiryId: InquiryId -> Task<Result<Envelope list, string>>
    abstract TryCurrentRevision: inquiryId: InquiryId -> Task<Revision option>

type IProviderPort =
    abstract Complete:
        inquiryId: InquiryId * prompt: JsonEnvelope * schemaRef: SchemaRef ->
            Task<Result<string * ProviderUsage, string>>

    abstract Cancel: inquiryId: InquiryId * requestRef: string -> Task<Result<Unit, string>>

type IDigestPort =
    abstract Sha256Hex: string -> string

type IClockPort =
    abstract Now: unit -> int64
