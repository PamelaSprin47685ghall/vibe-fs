namespace Wanxiangshu.OpenCode.Host

open Fable.Core
open Fable.Core.JsInterop

module ManagerReviewContract =

    /// host-boundary-032: one static stash definition for the review contract
    /// field. The deletion-failure compensation stays on: a failed hide
    /// deletes the saved record again before the error propagates.
    let private stash: ProtocolArgumentStash.StashSpec =
        { Symbol = emitJsExpr () "Symbol('manager-review-contract')"
          Fields =
            [ { Name = "contract"
                SavedKey = "descriptor" } ]
          ReappearanceFields = [ "contract" ]
          HoldMessage = "Tool arguments cannot hold or modify the review contract"
          RestoreOrderMessage = "Tool arguments cannot restore the review contract's original key order"
          RestoreFrozenMessage = "Tool arguments are frozen or not extensible during contract restore"
          FieldDeleteFailure = fun _ -> "Review contract could not be hidden"
          RestoreDeleteFailure =
            fun field ->
                if field = "contract" then
                    "Failed to delete contract property during restore"
                else
                    "Failed to restore review argument key order"
          SymbolDeleteFailure = "Failed to delete saved review contract key"
          CompensateDeleteFailure = true }

    let hide (args: obj) : unit = ProtocolArgumentStash.hide stash args

    let hideForCall (owner: ProtocolArgumentCall option) (args: obj) : unit =
        ProtocolArgumentStash.hideForCall stash owner args

    let classifyHiddenArguments (owner: ProtocolArgumentCall option) (args: obj) : HiddenProtocolArguments =
        ProtocolArgumentStash.classifyHiddenArguments stash owner args

    let restore (args: obj) : unit =
        ProtocolArgumentStash.restore stash args

    let restoreForCall (owner: ProtocolArgumentCall option) (args: obj) : unit =
        ProtocolArgumentStash.restoreForCall stash owner args
