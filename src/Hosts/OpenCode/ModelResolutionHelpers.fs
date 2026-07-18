module Wanxiangshu.Hosts.Opencode.ModelResolutionHelpers

open Fable.Core
open Wanxiangshu.Runtime.Dyn

let internal defaultOutputReserve = 32000

/// Extract {input/context, output} limit from a model definition in the provider catalog.
/// Prefers an explicit `input` limit when present; otherwise falls back to `context`.
/// The returned `output` value lets the caller reserve output headroom.
let extractLimitFromCatalogEntry (modelDef: obj) : (int * int) option =
    if isNullish modelDef then
        None
    else
        let limitObj = get modelDef "limit"

        if isNullish limitObj then
            None
        else
            let ctxVal = get limitObj "context"
            let outVal = get limitObj "output"
            let inputVal = get limitObj "input"

            let inputTokens =
                if not (isNullish inputVal) && typeIs inputVal "number" then
                    int (unbox<float> inputVal)
                else
                    0

            let contextTokens =
                if not (isNullish ctxVal) && typeIs ctxVal "number" then
                    int (unbox<float> ctxVal)
                else
                    0

            let outputTokens =
                if not (isNullish outVal) && typeIs outVal "number" then
                    int (unbox<float> outVal)
                else
                    0

            if inputTokens > 0 then
                Some(inputTokens, outputTokens)
            elif contextTokens > 0 then
                Some(contextTokens, outputTokens)
            else
                None

let private modelEntries (providerEntry: obj) : obj seq =
    let modelsObj = get providerEntry "models"

    if isNullish modelsObj then
        Seq.empty
    elif isArray modelsObj then
        unbox<obj array> modelsObj :> obj seq
    else
        Wanxiangshu.Runtime.Dyn.keys modelsObj |> Array.map (fun k -> get modelsObj k) :> obj seq

/// Find a model definition in the provider catalog by providerID and modelID.
/// Supports both flat model arrays (`data`/`all`) and provider catalogs where each
/// provider has a nested `models` record.
let findModelInCatalog (catalogData: obj) (providerID: string) (modelID: string) : obj option =
    if isNullish catalogData then
        None
    else
        let candidates =
            if isArray catalogData then
                catalogData :?> obj array
            else
                let fromKey key =
                    let inner = get catalogData key

                    if not (isNullish inner) && isArray inner then
                        Some(inner :?> obj array)
                    else
                        None

                match fromKey "data" with
                | Some arr -> arr
                | None ->
                    match fromKey "all" with
                    | Some arr -> arr
                    | None -> [||]

        candidates
        |> Array.tryPick (fun entry ->
            // Direct model entry (e.g. v2 model.list data array).
            let pId = str entry "providerID"
            let mId = str entry "id"

            if pId = providerID && mId = modelID then
                Some entry
            else
                // Provider entry with nested `models` (e.g. provider.list all array).
                entry
                |> modelEntries
                |> Seq.tryPick (fun m ->
                    let mpId = str m "providerID"
                    let mmId = str m "id"
                    let effectivePId = if mpId <> "" then mpId else str entry "id"

                    if effectivePId = providerID && mmId = modelID then
                        Some m
                    else
                        None))

/// Compute usable input tokens from context and output limits.
/// When outputTokens is 0 or missing, reserve the default output headroom.
let computeUsableInputTokens (contextTokens: int) (outputTokens: int) : int =
    let reserve =
        if outputTokens <= 0 then
            defaultOutputReserve
        else
            min outputTokens defaultOutputReserve

    max 0 (contextTokens - reserve)
