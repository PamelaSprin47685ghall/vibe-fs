module Wanxiangshu.Runtime.ContextBudgetResolve

open Fable.Core
open Wanxiangshu.Runtime.ContextBudgetLimitResolver
open Wanxiangshu.Runtime.Dyn

let modelFamilyLimit (modelId: string) : int option =
    let m = modelId.ToLowerInvariant()

    if
        m.Contains("gpt-4")
        || m.Contains("gpt-3.5")
        || m.Contains("claude")
        || m.Contains("gemini")
    then
        if m.Contains("gpt-3.5") then
            Some 16385
        elif
            m.Contains("gpt-4o")
            || m.Contains("gpt-4-turbo")
            || m.Contains("claude-3")
            || m.Contains("gemini-1.5")
        then
            Some 128000
        else
            Some 8192
    else
        None

let tryGetHostConfigDefault (target: obj) : int option =
    if isNullish target then
        None
    else
        let config = get target "config"

        if isNullish config then
            None
        else
            let defaultLimit = get config "defaultContextLimit"

            if not (isNullish defaultLimit) && typeIs defaultLimit "number" then
                Some(int (unbox<float> defaultLimit))
            else
                None

let private tryResolveFromSyncResults (syncRes: LimitResolution list) : int option =
    let syncInputOpt =
        syncRes
        |> List.tryPick (function
            | InputLimit v -> Some v
            | _ -> None)

    match syncInputOpt with
    | Some limit -> Some limit
    | None ->
        let syncContextOpt =
            syncRes
            |> List.tryPick (function
                | ContextLimit v -> Some v
                | _ -> None)

        syncContextOpt

let private tryResolveAsyncLimits (targets: obj list) (sessionID: string) (directory: string) : JS.Promise<int option> =
    promise {
        let mutable asyncInputOpt = None
        let mutable asyncContextOpt = None

        for t in targets do
            if asyncInputOpt.IsNone then
                let! limitRes = tryGetMaxInputTokensAsyncDetailed t sessionID directory

                match limitRes with
                | Some(InputLimit v) -> asyncInputOpt <- Some v
                | Some(ContextLimit v) when asyncContextOpt.IsNone -> asyncContextOpt <- Some v
                | _ -> ()

        return asyncInputOpt |> Option.orElse asyncContextOpt
    }

let private tryResolveFamilyLimit (targets: obj list) (sessionID: string) : JS.Promise<int option> =
    promise {
        let mutable familyLimitOpt = None

        for t in targets do
            if familyLimitOpt.IsNone then
                let! modelRefOpt = tryGetSessionModelRef t sessionID

                match modelRefOpt with
                | Some(modelId, _) -> familyLimitOpt <- modelFamilyLimit modelId
                | None -> ()

        return familyLimitOpt
    }

let resolveMaxInputTokens (targets: obj list) (sessionID: string) (directory: string) : JS.Promise<int> =
    promise {
        let syncRes = targets |> List.map tryExtractMaxInputTokensDetailed |> List.choose id

        match tryResolveFromSyncResults syncRes with
        | Some limit -> return limit
        | None ->
            let! asyncLimitOpt = tryResolveAsyncLimits targets sessionID directory

            match asyncLimitOpt with
            | Some limit -> return limit
            | None ->
                let! familyLimitOpt = tryResolveFamilyLimit targets sessionID

                match familyLimitOpt with
                | Some limit -> return limit
                | None ->
                    let hostDefaultOpt = targets |> List.tryPick tryGetHostConfigDefault

                    match hostDefaultOpt with
                    | Some limit -> return limit
                    | None ->
                        // 5. Global conservative default (8k)
                        return 8192
    }
