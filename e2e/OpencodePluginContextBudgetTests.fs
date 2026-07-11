module Wanxiangshu.E2e.OpencodePluginContextBudgetTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.OpencodePluginTestsPart2

module Dyn = Wanxiangshu.Shell.Dyn

let run
    (_harness: Harness)
    (chk: string -> bool -> unit)
    (startHarness: obj -> JS.Promise<obj>)
    (createEmpty: unit -> obj)
    : JS.Promise<int> =
    promise {
        // Context-budget nudge: high token usage near 75% window limit
        let mutable cbPromptCalls = 0
        let mutable cbPromptText = ""

        let cbOpts =
            createObj
                [ "sessionId", box "sess-cb"
                  "messages",
                  box
                      [| box (
                             createObj
                                 [ "info",
                                   box (
                                       createObj
                                           [ "role", box "user"
                                             "id", box "user-turn-1"
                                             "agent", box "build"
                                             "sessionID", box "sess-cb" ]
                                   )
                                   "parts",
                                   box [| box (createObj [ "type", box "text"; "text", box "initial user message" ]) |] ]
                         ) |]
                  "mockSessionClient",
                  box (
                      createObj
                          [ "get",
                            box (fun (arg: obj) ->
                                promise {
                                    let sid = Dyn.str arg "sessionID"
                                    chk "cb.mockSession.getCalled" (sid <> "")

                                    return
                                        box
                                            {| data =
                                                {| tokens =
                                                    {| input = 120000.0
                                                       cache = {| read = 0.0 |} |}
                                                   model =
                                                    {| id = "test-model"
                                                       providerID = "test"
                                                       limit = {| input = 200000.0 |} |} |} |}
                                })
                            "prompt",
                            box (fun (arg: obj) ->
                                cbPromptCalls <- cbPromptCalls + 1

                                let body = Dyn.get arg "body"

                                if not (Dyn.isNullish body) then
                                    let parts = Dyn.get body "parts"

                                    if not (Dyn.isNullish parts) && Dyn.isArray parts then
                                        let partsArr = unbox<obj array> parts

                                        if partsArr.Length > 0 then
                                            cbPromptText <- Dyn.str partsArr.[0] "text"

                                Promise.lift (box {| ok = true |})) ]
                  ) ]

        let! cbHarnessObj = withTimeout (startHarness cbOpts)
        let cbHarness = unbox<Harness> cbHarnessObj

        // Run transform with an agent/sessionID plan so MaxInputTokens=200000
        // and GetContextUsage returns 120000 (near 75% threshold).
        let transformPlan = createObj [ "agent", box "build"; "sessionID", box "sess-cb" ]

        let userMsg =
            createObj
                [ "info",
                  box (
                      createObj
                          [ "role", box "user"
                            "agent", box "build"
                            "sessionID", box "sess-cb"
                            "id", box "user-1" ]
                  )
                  "parts", box [| box (createObj [ "type", box "text"; "text", box "initial user message" ]) |] ]

        let! transformed = withTimeout (cbHarness.runMessageTransform transformPlan [| userMsg |])

        let msgsOut: obj array =
            if Dyn.isNullish transformed then
                [||]
            else
                unbox (Dyn.get transformed "messages")

        // Find the synthetic context-budget-nudge message
        let mutable foundNudge = false
        let mutable nudgeId = ""

        let mutable scan = 0

        while scan < msgsOut.Length && not foundNudge do
            let msg = msgsOut.[scan]
            let info = Dyn.get msg "info"
            let idVal = if Dyn.isNullish info then "" else Dyn.str info "id"

            if idVal.StartsWith("context-budget-nudge-") then
                foundNudge <- true
                nudgeId <- idVal

                let role = if Dyn.isNullish info then "" else Dyn.str info "role"
                chk "cb.nudgeRoleIsUser" (role = "user")

                let parts = Dyn.get msg "parts"

                if not (Dyn.isNullish parts) && Dyn.isArray parts then
                    let partsArr = unbox<obj array> parts

                    if partsArr.Length > 0 then
                        let text = Dyn.str partsArr.[0] "text"
                        chk "cb.nudgeTextContainsSuspended" (text.Contains("suspended"))

            scan <- scan + 1


        chk "cb.nudgeInjected" foundNudge
        chk "cb.nudgeIdPrefix" (nudgeId <> "")

        // Second transform simulates next host hook round: stripSynthetic removes prior nudge;
        // budget still hot → must inject again (regression for flaky missing nudge).
        let userMsg2 =
            createObj
                [ "info",
                  box (
                      createObj
                          [ "role", box "user"
                            "agent", box "build"
                            "sessionID", box "sess-cb"
                            "id", box "user-2" ]
                  )
                  "parts", box [| box (createObj [ "type", box "text"; "text", box "follow-up user message" ]) |] ]

        let! transformed2 = withTimeout (cbHarness.runMessageTransform transformPlan [| userMsg2 |])

        let msgsOut2: obj array =
            if Dyn.isNullish transformed2 then
                [||]
            else
                unbox (Dyn.get transformed2 "messages")

        let mutable foundNudge2 = false
        scan <- 0

        while scan < msgsOut2.Length && not foundNudge2 do
            let msg = msgsOut2.[scan]
            let info = Dyn.get msg "info"
            let idVal = if Dyn.isNullish info then "" else Dyn.str info "id"

            if idVal.StartsWith("context-budget-nudge-") then
                foundNudge2 <- true

            scan <- scan + 1

        chk "cb.nudgeInjectedSecondTransform" foundNudge2

        do! withTimeout (cbHarness.dispose ())

        return 0
    }
