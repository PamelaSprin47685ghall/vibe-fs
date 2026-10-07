namespace Wanxiangshu.Sphinx.V2.Wire

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Composition
open Wanxiangshu.Sphinx.V2.Hosts

module Surface =

    [<Literal>]
    let apiVersion = "2"

    let isTool (name: string) : bool = Contract.isTool name

    let private refused (refusal: ToolRefusal) : obj =
        createObj
            [ "apiVersion" ==> apiVersion
              "outcome" ==> "refused"
              "refusal"
              ==> createObj
                      [ "code" ==> refusal.Code
                        "path" ==> refusal.Path
                        "message" ==> refusal.Message ] ]

    let private result (value: Result<obj, ToolRefusal>) : obj =
        match value with
        | Ok payload -> payload
        | Error refusal -> refused refusal

    let private bindAppendCutUnknown (incident: AppendCutUnknownIncident) =
        let message =
            sprintf
                "inquiry %s; command %s; event %s: %s"
                (InquiryId.value incident.InquiryId)
                incident.CommandId
                (Wanxiangshu.Foundation.Identity.EventId.value incident.EventId)
                (AppendError.describe (AppendError.CommitUnknown incident.Evidence))

        FatalProcess.trip "sphinx-semantic-cut" message

    let create (commonDir: string) (writerId: string) (configuration: obj) : RuntimeHandle =
        match Bind.createDurableStore commonDir writerId with
        | Error reason -> raise (InvalidOperationException reason)
        | Ok store ->
            match
                Commands.create store (if isNull configuration then None else Some configuration) bindAppendCutUnknown
            with
            | Ok handle -> handle
            | Error refusal ->
                raise (InvalidOperationException(sprintf "%s at %s: %s" refusal.Code refusal.Path refusal.Message))

    let dispose (handle: RuntimeHandle) : unit = Commands.dispose handle

    let start (handle: RuntimeHandle) (args: obj) : Task<obj> =
        task {
            match Tool.decodeStart args with
            | Error refusal -> return refused refusal
            | Ok decoded ->
                let! started = Commands.start handle decoded
                return result started
        }

    let status (handle: RuntimeHandle) (args: obj) : obj =
        Tool.decodeStatus args |> Result.bind (Commands.status handle) |> result

    let exportInquiry (handle: RuntimeHandle) (args: obj) : obj =
        Tool.decodeExport args |> Result.bind (Commands.exportInquiry handle) |> result

    let submitResults (handle: RuntimeHandle) (args: obj) : obj =
        Commands.store handle |> ignore

        Tool.decodeWorkSubmit args
        |> Result.bind (fun _ -> Error(Tool.unsupported (Contract.toolName SphinxTool.WorkSubmit)))
        |> result
