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

type DispatchReceipt =
    { DispatchIntentId: string
      PhysicalRef: string
      Receipt: string }

type PhysicalStatus =
    | Running
    | Succeeded
    | Failed of reason: string
    | Cancelled
    | Unknown

type IHostPort =
    abstract Capabilities: unit -> string list

    abstract Dispatch:
        inquiryId: InquiryId * work: WorkSpec * publicEnvelope: JsonEnvelope * privateTicket: JsonEnvelope ->
            Task<Result<DispatchReceipt, string>>

    abstract ReadStatus:
        inquiryId: InquiryId * workId: WorkId * attempt: Attempt * physicalRef: string -> Task<PhysicalStatus>

    abstract ReadResult:
        inquiryId: InquiryId * workId: WorkId * attempt: Attempt * physicalRef: string ->
            Task<Result<string option, string>>

    abstract RequestCancel:
        inquiryId: InquiryId * workId: WorkId * attempt: Attempt * physicalRef: string -> Task<Result<Unit, string>>

    /// Re-open the question "does this dispatch exist?" after a crash window.
    abstract Reconcile: inquiryId: InquiryId * dispatchIntentId: string -> Task<Result<string option, string>>

type ProviderUsage =
    { InputTokens: int64
      OutputTokens: int64
      Calls: int64
      MoneyMinor: int64
      UsageUnresolved: bool }

type IProviderPort =
    abstract Complete:
        inquiryId: InquiryId * prompt: JsonEnvelope * schemaRef: SchemaRef ->
            Task<Result<string * ProviderUsage, string>>

    abstract Cancel: inquiryId: InquiryId * requestRef: string -> Task<Result<Unit, string>>

type IDigestPort =
    abstract Sha256Hex: string -> string

type IClockPort =
    abstract Now: unit -> int64
