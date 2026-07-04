module Wanxiangshu.Tests.TitleFetchGuardTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

open Wanxiangshu.Shell.TitleFetchGuardCommon
open Wanxiangshu.Shell.Dyn

let signature () =
    equal "title probe signature pinned to opencode ensureTitle string"
        "Generate a title for this conversation" titleRequestSignature

let wrap () =
    let wrapped = wrapForTitle "重构用户服务"
    check "wrap embeds original inside do-not-exec tag"
        (wrapped.Contains "<do-not-exec>重构用户服务</do-not-exec>")
    check "wrap asks for naming only" (wrapped.Contains "Please name the conversation")
    check "wrap forbids execution" (wrapped.Contains "should not actually execute")
    check "wrap requests precise style" (wrapped.Contains "precise Chinese")

let detect () =
    let titleBody = box "{\"messages\":[{\"role\":\"user\",\"content\":\"Generate a title for this conversation\\n\"}]}"
    let plainBody = box "{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}"
    check "title body detected" (isTitleRequestBody titleBody)
    check "plain body not detected" (not (isTitleRequestBody plainBody))
    check "non-string body not detected" (not (isTitleRequestBody (createObj [ "x", box 1 ])))
    check "null body not detected" (not (isTitleRequestBody null))
    let systemMention = box "{\"system\":\"Generate a title for this conversation is done elsewhere\",\"messages\":[{\"role\":\"user\",\"content\":\"你好\"}]}"
    check "system mention not detected" (not (isTitleRequestBody systemMention))
    let historyMention = box "{\"messages\":[{\"role\":\"user\",\"content\":\"你好\"},{\"role\":\"user\",\"content\":\"Generate a title for this conversation\"}]}"
    check "probe not in messages[0] not detected" (not (isTitleRequestBody historyMention))
    let nonUserFirst = box "{\"messages\":[{\"role\":\"system\",\"content\":\"Generate a title for this conversation\"}]}"
    check "probe in non-user messages[0] not detected" (not (isTitleRequestBody nonUserFirst))
    let realTitleBody = box "{\"messages\":[{\"role\":\"system\",\"content\":\"You are a title generator.\"},{\"role\":\"user\",\"content\":\"Generate a title for this conversation:\\n\"},{\"role\":\"user\",\"content\":\"真实需求\"}]}"
    check "system-prefixed title body detected" (isTitleRequestBody realTitleBody)

let tryWrapString () =
    equal "string content wrapped" (Some(wrapForTitle "x")) (tryWrapStringContent (box "x"))
    equal "string content round-trips text" (wrapForTitle "你好") (tryWrapStringContent (box "你好") |> Option.defaultValue "")
    check "array content returns None" ((tryWrapStringContent (box [| "a" |])).IsNone)

let rewriteStringContent () =
    let msg = createObj [ "role", box "user"; "content", box "原始需求" ]
    let parsed = createObj [ "messages", box [| msg |] ]
    rewriteTitleMessages parsed
    equal "string content rewritten in place" (wrapForTitle "原始需求") (str msg "content")

let rewriteArrayContent () =
    let part = createObj [ "type", box "text"; "text", box "你好" ]
    let msg = createObj [ "role", box "user"; "content", box [| part |] ]
    let parsed = createObj [ "messages", box [| msg |] ]
    rewriteTitleMessages parsed
    equal "array text part rewritten in place" (wrapForTitle "你好") (str part "text")

let skipProbeMessage () =
    let probe = createObj [ "role", box "user"; "content", box "Generate a title for this conversation:\n" ]
    let userMsg = createObj [ "role", box "user"; "content", box "真实需求" ]
    let parsed = createObj [ "messages", box [| probe; userMsg |] ]
    rewriteTitleMessages parsed
    equal "probe message left intact" "Generate a title for this conversation:\n" (str probe "content")
    equal "non-probe user still wrapped" (wrapForTitle "真实需求") (str userMsg "content")
