module Wanxiangshu.Tests.IntegrationEventTestsOpencodeFallback

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn

let fallbackRetryWithoutFrontmatterSpec () = promise {
    let promptCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise { promptCalls.Add(arg) }))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                promise {
                    return box {| data = [|
                        box {| info = box {| role = "assistant"; agent = "reviewer"; model = box {| providerID = "openai"; modelID = "gpt-5" |} |}
                               parts = [| box {| ``type`` = "text"; text = "working" |} |] |}
                    |] |}
                }))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
        ]) ]
    let! workspaceDir = mkdtempAsync "fallback-retry-no-frontmatter-"
    let! p = plugin (box {| directory = workspaceDir; client = mkClient () |})
    let eventHook = get p "event"
    let sid = "fallback-no-frontmatter-session"
    do! eventHook $ (box {| event = box {| ``type`` = "session.status"; properties = box {| info = box {| sessionID = sid |}; status = box {| ``type`` = "busy"; agent = "reviewer" |} |} |} |}) |> unbox<JS.Promise<unit>>
    do! eventHook $ (box {| event = box {| ``type`` = "session.error"; properties = box {| info = box {| sessionID = sid |}; error = box {| name = "APIError"; message = "boom"; isRetryable = true |} |} |} |}) |> unbox<JS.Promise<unit>>
    do! Promise.sleep 0
    equal "fallback without frontmatter retries once" 1 promptCalls.Count
    let call = promptCalls.[0]
    equal "retry path targets same session" sid (str (get call "path") "id")
    equal "retry body keeps current agent" "reviewer" (str (get call "body") "agent")
    equal "retry body keeps current model provider" "openai" (str (get (get call "body") "model") "providerID")
    equal "retry body keeps current model id" "gpt-5" (str (get (get call "body") "model") "modelID")
    do! rmAsync workspaceDir
}
