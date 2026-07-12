module Wanxiangshu.Shell.ContextBudgetResolve

open Fable.Core
open Wanxiangshu.Shell.ContextBudgetLimitResolver
open Wanxiangshu.Shell.Dyn

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

            match syncContextOpt with
            | Some limit -> return limit
            | None ->
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
                    | None ->
                        // 3. Model family known conservative limit
                        let mutable familyLimitOpt = None

                        for t in targets do
                            if familyLimitOpt.IsNone then
                                let! modelRefOpt = tryGetSessionModelRef t sessionID

                                match modelRefOpt with
                                | Some(modelId, _) -> familyLimitOpt <- modelFamilyLimit modelId
                                | None -> ()

                        match familyLimitOpt with
                        | Some limit -> return limit
                        | None ->
                            // 4. Host config default
                            let hostDefaultOpt = targets |> List.tryPick tryGetHostConfigDefault

                            match hostDefaultOpt with
                            | Some limit -> return limit
                            | None ->
                                // 5. Global conservative default (8k)
                                return 8192
    }
