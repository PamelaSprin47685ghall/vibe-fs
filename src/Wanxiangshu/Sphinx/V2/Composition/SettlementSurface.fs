namespace Wanxiangshu.Sphinx.V2.Composition

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Hosts

module SettlementSurface =
    [<Emit("$0 === $1")>]
    let private sameReference (left: obj) (right: obj) : bool = jsNative

    let private matchesAppendEvidence (incident: AppendCutUnknownIncident) (originalError: obj) =
        match unbox<AppendError> originalError with
        | AppendError.CommitUnknown evidence -> sameReference (box incident.Evidence) (box evidence)
        | _ -> false

    let private incidentView (incident: AppendCutUnknownIncident) : obj =
        createObj
            [ "incident" ==> incident
              "inquiryId" ==> InquiryId.value incident.InquiryId
              "commandId" ==> incident.CommandId
              "eventId" ==> Wanxiangshu.Foundation.Identity.EventId.value incident.EventId
              "cause" ==> incident.Evidence.Primary.Cause
              "matchesAppendEvidence"
              ==> (fun originalError -> matchesAppendEvidence incident originalError) ]

    let create (store: EventStoreHandle) (configuration: obj) (onIncident: obj -> unit) : RuntimeHandle =
        let raw = if isNull configuration then None else Some configuration

        match Commands.create store.Store raw (incidentView >> onIncident) with
        | Ok handle -> handle
        | Error refusal ->
            raise (InvalidOperationException(sprintf "%s at %s: %s" refusal.Code refusal.Path refusal.Message))

    let private refused (refusal: ToolRefusal) : obj =
        box
            {| ok = false
               error =
                box
                    {| code = refusal.Code
                       path = refusal.Path
                       message = refusal.Message |} |}

    let private result (value: Result<obj, ToolRefusal>) : obj =
        match value with
        | Ok payload -> box {| ok = true; value = payload |}
        | Error refusal -> refused refusal

    let start (handle: RuntimeHandle) (args: obj) : Task<obj> =
        task {
            match Tool.decodeStart args with
            | Error refusal -> return refused refusal
            | Ok decoded ->
                let! started = Commands.start handle decoded
                return result started
        }

    let settleAppendCutUnknown
        (handle: RuntimeHandle)
        (incident: AppendCutUnknownIncident)
        (originalError: obj)
        : unit =
        Commands.settleAppendCutUnknown
            handle
            incident.InquiryId
            incident.CommandId
            incident.EventId
            (unbox<AppendError> originalError)

    let dispose (handle: RuntimeHandle) : unit = Commands.dispose handle
