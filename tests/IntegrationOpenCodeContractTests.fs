module Wanxiangshu.Tests.IntegrationOpencodeContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests
open Wanxiangshu.Integration.OpencodePluginNudgeForceStopContractTests
open Wanxiangshu.Integration.OpencodePluginStreamAbortContractTests
open Wanxiangshu.Integration.OpencodeContinueContractTests
open Wanxiangshu.Integration.OpencodeNudgeHostIntegrationTests

[<Emit("JSON.stringify($0)")>]
let private jsonStringify (o: obj) : string = jsNative

[<Import("start", "../integration/harness.js")>]
let private startHarness: obj -> JS.Promise<obj> = jsNative

let private createEmpty () = createObj []

let private dynGet (o: obj) (k: string) : obj = Wanxiangshu.Runtime.Dyn.get o k
let private dynIsNull (o: obj) : bool = Wanxiangshu.Runtime.Dyn.isNullish o
let private dynStr (o: obj) (k: string) : string = Wanxiangshu.Runtime.Dyn.str o k

let private objectKeys (o: obj) : string array =
    JS.Constructors.Object.keys (o) |> unbox

let runAll (args: string array) : JS.Promise<unit> =
    promise {
        let mutable totalFailed = 0

        let chk label cond =
            check label cond

            if not cond then
                totalFailed <- totalFailed + 1

        // --- plugin tools, system transform, lifecycle hooks, tool.execute.before/after ---
        let execArgs =
            createObj
                [ "command", box "echo test"
                  "language", box "shell"
                  "timeout_type", box "short" ]

        let toolLifecycleOpts =
            createObj
                [ "agentsContent", box "---\nmodels:\n  default:\n    - test/test-model\n---\n"
                  "mockSessionClient",
                  box (
                      createObj
                          [ "todo", box (fun _ -> Promise.lift (box {| data = [||] |}))
                            "prompt", box (fun _ -> Promise.lift (box {| ok = true |}))
                            "model",
                            box
                                {| id = "test-model"
                                   providerID = "test" |} ]
                  ) ]

        let! h2obj = withTimeoutCustom 30000 (startHarness toolLifecycleOpts)

        let harness2 =
            unbox<Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests.Harness> h2obj

        do! runToolLifecycle harness2 chk "test-warn-tdd" "test-warn" execArgs createEmpty dynGet dynIsNull dynStr

        let pluginKeys = harness2.getPlugin () |> objectKeys
        let forbiddenKeys = pluginKeys |> Array.filter (fun k -> k.StartsWith "__")
        chk "opencode public plugin must not expose __-prefixed keys" (forbiddenKeys.Length = 0)

        do! withTimeoutCustom 4000 (harness2.dispose ())

        // --- nudge & force-stop ---
        let nudgeForceStopOpts =
            createObj
                [ "agentsContent", box "---\nmodels:\n  default:\n    - test/test-model\n---\n"
                  "messages",
                  box
                      [| box (
                             createObj
                                 [ "info",
                                   box (
                                       createObj
                                           [ "role", box "assistant"
                                             "agent", box "build"
                                             "finish", box "stop"
                                             "id", box "msg-1"
                                             "time", box {| completed = 1000.0 |} ]
                                   )
                                   "parts", box [| box (createObj [ "type", box "text"; "text", box "reply" ]) |] ]
                         ) |]
                  "mockSessionClient",
                  box (
                      createObj
                          [ "todo", box (fun _ -> Promise.lift (box {| data = [||] |}))
                            "prompt",
                            box (fun (body: obj) ->
                                jsonStringify body |> ignore
                                Promise.lift (box {| ok = true |}))
                            "model",
                            box
                                {| id = "test-model"
                                   providerID = "test" |} ]
                  ) ]

        let! h3obj = withTimeoutCustom 30000 (startHarness nudgeForceStopOpts)

        let harness3 =
            unbox<Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests.Harness> h3obj

        do! runNudgeForceStop harness3 chk startHarness jsonStringify createEmpty
        do! withTimeoutCustom 4000 (harness3.dispose ())

        // --- stream-abort + lifecycle ---
        let streamAbortOpts =
            createObj
                [ "agentsContent", box "---\nmodels:\n  default:\n    - test/test-model\n---\n"
                  "mockSessionClient",
                  box (
                      createObj
                          [ "todo", box (fun _ -> Promise.lift (box {| data = [||] |}))
                            "prompt", box (fun _ -> Promise.lift (box {| ok = true |}))
                            "model",
                            box
                                {| id = "test-model"
                                   providerID = "test" |} ]
                  ) ]

        let! h4obj = withTimeoutCustom 30000 (startHarness streamAbortOpts)

        let harness4 =
            unbox<Wanxiangshu.Integration.OpencodePluginToolLifecycleContractTests.Harness> h4obj

        do! runStreamAbort harness4 chk startHarness jsonStringify createEmpty
        do! withTimeoutCustom 4000 (harness4.dispose ())

        // --- Continue: continuation after tool-complete + network error ---
        do! Wanxiangshu.Integration.OpencodeContinueContractTests.run startHarness chk createEmpty

        // --- Nudge: fallback-continue, bug1, bug2, abort suppression ---
        let nudgeopts =
            createObj
                [ "agentsContent", box "---\nmodels:\n  default:\n    - test/test-model\n---\n"
                  "mockSessionClient",
                  box (
                      createObj
                          [ "todo", box (fun _ -> Promise.lift (box {| data = [||] |}))
                            "prompt", box (fun _ -> Promise.lift (box {| ok = true |}))
                            "create", box (fun _ -> Promise.lift (box {| data = {| id = "mock-review" |} |}))
                            "model",
                            box
                                {| id = "test-model"
                                   providerID = "test" |} ]
                  ) ]

        let! _nudgeResult =
            withTimeoutCustom
                60000
                (Wanxiangshu.Integration.OpencodeNudgeHostIntegrationTests.runNudgeTests
                    harness4
                    chk
                    startHarness
                    jsonStringify
                    0
                    (fun () -> totalFailed)
                    createEmpty)

        return ()
    }
