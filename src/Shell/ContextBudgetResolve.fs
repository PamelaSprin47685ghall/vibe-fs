module Wanxiangshu.Shell.ContextBudgetResolve

open Fable.Core
open Wanxiangshu.Shell.ContextBudgetLimitResolver

let resolveMaxInputTokens (targets: obj list) (sessionID: string) (directory: string) : JS.Promise<int> =
    promise {
        let syncRes = targets |> List.map tryExtractMaxInputTokensDetailed |> List.choose id

        let syncInputOpt =
            syncRes
            |> List.tryPick (function
                | InputLimit v -> Some v
                | _ -> None)

        match syncInputOpt with
        | Some limit -> return limit
        | None ->
            let syncContextOpt =
                syncRes
                |> List.tryPick (function
                    | ContextLimit v -> Some v
                    | _ -> None)

            let mutable asyncInputOpt = None
            let mutable asyncContextOpt = None

            for t in targets do
                if asyncInputOpt.IsNone then
                    let! limitRes = tryGetMaxInputTokensAsyncDetailed t sessionID directory

                    match limitRes with
                    | Some(InputLimit v) -> asyncInputOpt <- Some v
                    | Some(ContextLimit v) when asyncContextOpt.IsNone -> asyncContextOpt <- Some v
                    | _ -> ()

            match asyncInputOpt with
            | Some limit -> return limit
            | None ->
                match asyncContextOpt with
                | Some limit -> return limit
                | None -> return Option.defaultValue 0 syncContextOpt
    }
