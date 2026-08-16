namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.OpenCode

/// Pure provider-input materialization owner. Snapshot/resource operations are
/// intentionally absent; those belong to MagicTodoMembraneSurface.
[<RequireQualifiedAccess>]
module MagicTodoLocalitySurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private stateOf (value: obj) : SnapshotToolPartState =
        match if isNull value then 0 else unbox<int> value with
        | 1 -> SnapshotToolPartState.Completed ""
        | 2 -> SnapshotToolPartState.Failed ""
        | _ -> SnapshotToolPartState.Pending

    let private cursor (sequence: int) : XTraceCursor = { Sequence = int64 sequence }

    let private localizedOf (callId: string) (inputCanonical: string) (state: obj) : MagicTodoLocality.LocalizedToolCall =
        let frontier = cursor 7

        { ProviderRun = ProviderRunIdentity.create "msg-provider-run"
          HostToolPartId = HostToolPartId.create "prt-todowrite"
          ToolCallId = ToolCallId.create callId
          ToolName = "todowrite"
          InputCanonical = inputCanonical
          State = stateOf state
          TodowriteCallIdsInMessage = [ ToolCallId.create callId ]
          ToolPartOrdinal = 1
          ReviewFrontier = frontier
          Range =
            { Start = frontier
              EndExclusive = cursor 8 } }

    let materializeInput (callId: string) (inputCanonical: string) (state: obj) (expectedCanonical: string) : obj =
        let localized = localizedOf callId inputCanonical state

        match MagicTodoLocality.materializeInput localized expectedCanonical with
        | Ok value -> box {| ok = true; value = box {| inputCanonical = value.InputCanonical |} |}
        | Error reason ->
            let code =
                match reason with
                | MagicTodoLocality.InputMaterializationRejection.SnapshotUnavailable _ -> "SnapshotUnavailable"
                | MagicTodoLocality.InputMaterializationRejection.Snapshot _ -> "Snapshot"
                | MagicTodoLocality.InputMaterializationRejection.CarrierChanged -> "CarrierChanged"
                | MagicTodoLocality.InputMaterializationRejection.InputMismatch -> "InputMismatch"

            box {| ok = false; error = box {| code = code |} |}
