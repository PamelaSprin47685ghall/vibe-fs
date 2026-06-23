module VibeFs.Shell.ErrorClassify

open Fable.Core
open VibeFs.Shell.Dyn
open VibeFs.Kernel.Domain

let translateJsError (error: obj) : DomainError =
    let rec classify (value: obj) (seen: obj list) =
        if isNullish value then SystemPanic "Null error context"
        elif List.exists (fun seenObj -> obj.ReferenceEquals(value, seenObj)) seen then SystemPanic "Cyclic error context"
        elif typeIs value "string" then
            classifyErrorLeaf "" "" (string value)
        else
            let seenNext = value :: seen
            let name = str value "name"
            let tag = str value "_tag"
            // empty message isolates name/_tag specific matches; only the fallback recurses
            match classifyErrorLeaf name tag "" with
            | MessageAborted | SessionBusy | TaskWaitBackgrounded as resolved -> resolved
            | _ ->
                let nested = get value "error"
                if not (isNullish nested) then classify nested seenNext
                else
                    let data = get value "data"
                    if not (isNullish data) && typeIs data "object" then classify data seenNext
                    else
                        let cause = get value "cause"
                        if not (isNullish cause) then classify cause seenNext
                        else classifyErrorLeaf name tag (str value "message")
    classify error []

let isAbortDomainError (error: obj) : bool =
    match translateJsError error with
    | MessageAborted -> true
    | _ -> false
