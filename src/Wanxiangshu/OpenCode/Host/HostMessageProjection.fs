namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop

/// The one Host-object mutation the Companion transform performs.
///
/// In-place, and that is not a style preference. `plugin.trigger` discards the hook's
/// return value (`plugin/index.ts:284-293`) and the Host then reads its original
/// `msgs` binding (`prompt.ts:1262`), so `output.messages = rewritten` is silently
/// ignored: the provider receives the untouched transcript while every assertion
/// passes. Confirmed against Host source (`plugin/index.ts` discard + `prompt.ts`
/// original `msgs` binding).
///
/// HOST-016: 对 provider-facing 消息做非空 content 兜底保障，避免仅 reasoning
/// 或空 content 在上游 API 报 messages[i].content cannot be empty 400 错误。
module HostMessageProjection =

    let private readField (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let replaceMessagesInPlace (rawOutObj: obj) (transformed: obj list) =
        emitJsExpr (rawOutObj?messages, List.toArray transformed) "$0.length = 0; $0.push(...$1);"
        |> ignore

    let sanitizeMessage (raw: obj) : obj =
        if isNull raw then
            raw
        else
            let role =
                let info = readField raw "info"
                let fromInfo = if isNull info then null else readField info "role"

                if isNull fromInfo then
                    let fromRaw = readField raw "role"
                    if isNull fromRaw then "" else unbox<string> fromRaw
                else
                    unbox<string> fromInfo

            let rawParts: obj array =
                let parts = readField raw "parts"
                if isNull parts then [||] else unbox<obj array> parts

            let hasNonEmptyText =
                let textFromParts =
                    rawParts
                    |> Array.exists (fun p ->
                        if isNull p then
                            false
                        else
                            let typeVal = readField p "type"

                            let kind =
                                if isNull typeVal then
                                    ""
                                else
                                    (unbox<string> typeVal).ToLowerInvariant()

                            let textVal = readField p "text"

                            kind = "text"
                            && not (isNull textVal)
                            && not (String.IsNullOrWhiteSpace(unbox<string> textVal)))

                let textFromTopLevel =
                    let contentVal = readField raw "content"

                    let fromContent =
                        if isNull contentVal then
                            false
                        else
                            emitJsExpr contentVal "typeof $0 === 'string'"
                            && not (String.IsNullOrWhiteSpace(unbox<string> contentVal))

                    let textVal = readField raw "text"

                    let fromText =
                        if isNull textVal then
                            false
                        else
                            not (String.IsNullOrWhiteSpace(unbox<string> textVal))

                    fromContent || fromText

                textFromParts || textFromTopLevel

            let hasTool =
                let toolFromParts =
                    rawParts
                    |> Array.exists (fun p ->
                        if isNull p then
                            false
                        else
                            let typeVal = readField p "type"

                            let kind =
                                if isNull typeVal then
                                    ""
                                else
                                    (unbox<string> typeVal).ToLowerInvariant()

                            kind = "tool"
                            || kind = "tool-call"
                            || kind = "tool_call"
                            || kind = "tool-result"
                            || kind = "tool_result")

                let toolCallsVal = readField raw "tool_calls"

                let toolFromTopLevel =
                    not (isNull toolCallsVal)
                    && emitJsExpr toolCallsVal "Array.isArray($0) && $0.length > 0"

                toolFromParts || toolFromTopLevel

            if hasNonEmptyText || hasTool then
                raw
            else
                let reasoningText =
                    rawParts
                    |> Array.tryPick (fun p ->
                        if isNull p then
                            None
                        else
                            let typeVal = readField p "type"

                            let kind =
                                if isNull typeVal then
                                    ""
                                else
                                    (unbox<string> typeVal).ToLowerInvariant()

                            if kind = "reasoning" || kind = "thinking" then
                                let textVal = readField p "text"
                                let reasoningVal = readField p "reasoning"
                                let thinkingVal = readField p "thinking"

                                let t =
                                    if not (isNull textVal) then
                                        unbox<string> textVal
                                    elif not (isNull reasoningVal) then
                                        unbox<string> reasoningVal
                                    elif not (isNull thinkingVal) then
                                        unbox<string> thinkingVal
                                    else
                                        ""

                                if not (String.IsNullOrWhiteSpace t) then Some t else None
                            else
                                None)

                let fallbackText =
                    match reasoningText with
                    | Some r -> r
                    | None -> if role.ToLowerInvariant() = "assistant" then "..." else "#"

                let textPart = createObj [ "type", box "text"; "text", box fallbackText ]

                let newParts = Array.append rawParts [| textPart |]
                let cloned = emitJsExpr raw "Object.assign({}, $0)"
                emitJsExpr (cloned, newParts) "$0.parts = $1" |> ignore
                cloned

    let sanitizeMessages (messages: obj list) : obj list = messages |> List.map sanitizeMessage
