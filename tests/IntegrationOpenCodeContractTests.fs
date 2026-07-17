module Wanxiangshu.Tests.IntegrationOpencodeContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Integration.OpencodePluginContractTestsPart2
open Wanxiangshu.Integration.OpencodePluginContractTestsPart3
open Wanxiangshu.Integration.OpencodePluginContractTestsPart4
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

let runAll (args: string array) : JS.Promise<unit> =
    promise {
        clearFailuresForRun ()
        let mutable totalFailed = 0

        let chk label cond =
            check label cond

            if not cond then
                totalFailed <- totalFailed + 1

        // --- Part2: plugin tools, system transform, lifecycle hooks, tool.execute.before/after ---
        let warnTddValue = "test-warn-tdd"
        let warnValue = "test-warn"

        let execArgs =
            createObj
                [ "command", box "echo test"
                  "language", box "shell"
                  "mode", box "ro"
                  "timeout_type", box "short"
                  "warn_tdd", box warnTddValue
                  "warn", box warnValue ]

        let part2opts =
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

        let! h2obj = withTimeoutCustom 30000 (startHarness part2opts)

        let harness2 =
            unbox<Wanxiangshu.Integration.OpencodePluginContractTestsPart2.Harness> h2obj

        do! runPart2 harness2 chk warnTddValue warnValue execArgs createEmpty dynGet dynIsNull dynStr
        do! withTimeoutCustom 4000 (harness2.dispose ())

        // --- Part3: nudge & force-stop ---
        let part3opts =
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

        let! h3obj = withTimeoutCustom 30000 (startHarness part3opts)

        let harness3 =
            unbox<Wanxiangshu.Integration.OpencodePluginContractTestsPart2.Harness> h3obj

        do! runPart3 harness3 chk startHarness jsonStringify createEmpty
        do! withTimeoutCustom 4000 (harness3.dispose ())

        // --- Part4: stream-abort + lifecycle ---
        let part4opts =
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

        let! h4obj = withTimeoutCustom 30000 (startHarness part4opts)

        let harness4 =
            unbox<Wanxiangshu.Integration.OpencodePluginContractTestsPart2.Harness> h4obj

        do! runPart4 harness4 chk startHarness jsonStringify createEmpty
        do! withTimeoutCustom 4000 (harness4.dispose ())

        // --- Continue: continuation after tool-complete + network error ---
        do! Wanxiangshu.Integration.OpencodeContinueContractTests.run startHarness chk createEmpty

        // --- Nudge: fallback-continue, bug1, bug2, abort suppression ---
        let nudgeopts =
            createObj
                [ "agentsContent",
                  box
                      "---\nmodels:\n  default:\n    - test/test-model\nfallback:\n  legacyZeroWidthContinue: true\n---\n"
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

        let _failed = summary ()
        return ()
    }
