module Wanxiangshu.Shell.ErrorClassify

open Fable.Core
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Domain

let translateJsError (error: obj) : DomainError =
    let strOpt (o: obj) (key: string) : string option =
        match opt o key with
        | Some v -> Some(string v)
        | None -> None

    let hasNonEmpty (o: obj) (key: string) : bool =
        match opt o key with
        | Some v -> not (isNullish v) && string v <> ""
        | None -> false

    let rec classify (value: obj) (seen: obj list) =
        if isNullish value then
            SystemPanic "Null error context"
        elif List.exists (fun seenObj -> obj.ReferenceEquals(value, seenObj)) seen then
            SystemPanic "Cyclic error context"
        elif typeIs value "string" then
            classifyErrorLeaf "" "" (string value)
        else
            let seenNext = value :: seen
            let name = str value "name"
            let tag = str value "_tag"

            match classifyErrorLeaf name tag "" with
            | MessageAborted
            | SessionBusy
            | TaskWaitBackgrounded
            | ClientCancellation _ as resolved -> resolved
            | _ ->
                let nested = get value "error"

                if not (isNullish nested) then
                    classify nested seenNext
                else
                    let data = get value "data"

                    if not (isNullish data) && typeIs data "object" then
                        classify data seenNext
                    else
                        let cause = get value "cause"

                        if not (isNullish cause) then
                            classify cause seenNext
                        else
                            let message = str value "message"
                            let config = get value "config"
                            let response = get value "response"
                            // AbortSignal: ABORT_ERR code or signal present
                            if strOpt value "code" = Some "ABORT_ERR" || hasNonEmpty value "signal" then
                                ClientCancellation "AbortSignal"
                            // FileSystem: path + errno
                            elif hasNonEmpty value "path" && hasNonEmpty value "errno" then
                                FileSystemFault(str value "path", str value "errno", message)
                            // Network: statusCode + config.url
                            elif
                                hasNonEmpty value "statusCode"
                                && not (isNullish config)
                                && hasNonEmpty config "url"
                            then
                                NetworkTransportFailure(
                                    str config "url",
                                    opt value "statusCode" |> Option.map unbox<int>,
                                    message
                                )
                            // Network: response.status + config.url
                            elif
                                not (isNullish response)
                                && hasNonEmpty response "status"
                                && not (isNullish config)
                                && hasNonEmpty config "url"
                            then
                                NetworkTransportFailure(
                                    str config "url",
                                    opt response "status" |> Option.map unbox<int>,
                                    message
                                )
                            // Protocol mismatch: field + expected + actual
                            elif
                                hasNonEmpty value "field"
                                && hasNonEmpty value "expected"
                                && hasNonEmpty value "actual"
                            then
                                HostProtocolMismatch(str value "field", str value "expected", str value "actual")
                            else
                                classifyErrorLeaf name tag message

    classify error []

let isAbortDomainError (error: obj) : bool =
    match translateJsError error with
    | MessageAborted -> true
    | _ -> false
