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
type RuntimeHandle =
    internal new: store: EventStoreHandle * configuration: AuthorizedStartConfiguration option -> RuntimeHandle

    member internal Store: IEventStore
    member internal Configuration: AuthorizedStartConfiguration option
    member internal Dispose: unit -> unit

[<RequireQualifiedAccess>]
module Commands =
    val create: IEventStore -> obj option -> Result<RuntimeHandle, ToolRefusal>
    val store: RuntimeHandle -> IEventStore
    val dispose: RuntimeHandle -> unit
    val start: RuntimeHandle -> StartArgs -> Task<Result<obj, ToolRefusal>>
    val status: RuntimeHandle -> StatusArgs -> Result<obj, ToolRefusal>
    val exportInquiry: RuntimeHandle -> ExportArgs -> Result<obj, ToolRefusal>
