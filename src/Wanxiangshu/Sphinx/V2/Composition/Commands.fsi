namespace Wanxiangshu.Sphinx.V2.Composition

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Hosts
open Wanxiangshu.Sphinx.V2.Runtime

type internal AuthorizedStartConfiguration =
    { CommandNamespace: string
      CreatedBy: string
      Profile: DefaultProfile
      ResourceSpecs: ResourceSpec list
      RenderReserve: Map<string, float> }

[<Sealed>]
type AppendCutUnknownIncident =
    internal new:
        inquiryId: InquiryId *
        commandId: string *
        eventId: Wanxiangshu.Foundation.Identity.EventId *
        evidence: AppendCommitUnknownEvidence ->
            AppendCutUnknownIncident

    member InquiryId: InquiryId
    member CommandId: string
    member EventId: Wanxiangshu.Foundation.Identity.EventId
    member Evidence: AppendCommitUnknownEvidence

[<Sealed>]
type RuntimeHandle =
    internal new:
        store: EventStoreHandle *
        configuration: AuthorizedStartConfiguration option *
        onAppendCutUnknown: (AppendCutUnknownIncident -> unit) ->
            RuntimeHandle

    member internal Store: IEventStore
    member internal Configuration: AuthorizedStartConfiguration option
    member internal Dispose: unit -> unit

    member internal SettleAppendCutUnknown:
        inquiryId: InquiryId *
        commandId: string *
        eventId: Wanxiangshu.Foundation.Identity.EventId *
        failure: AppendError ->
            unit

[<RequireQualifiedAccess>]
module Commands =
    val create: IEventStore -> obj option -> (AppendCutUnknownIncident -> unit) -> Result<RuntimeHandle, ToolRefusal>
    val store: RuntimeHandle -> IEventStore
    val dispose: RuntimeHandle -> unit

    val settleAppendCutUnknown:
        RuntimeHandle -> InquiryId -> string -> Wanxiangshu.Foundation.Identity.EventId -> AppendError -> unit

    val start: RuntimeHandle -> StartArgs -> Task<Result<obj, ToolRefusal>>
    val status: RuntimeHandle -> StatusArgs -> Result<obj, ToolRefusal>
    val exportInquiry: RuntimeHandle -> ExportArgs -> Result<obj, ToolRefusal>
