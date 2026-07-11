module Wanxiangshu.E2e.OpencodePluginContextBudgetTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Tests.Assert
open Wanxiangshu.E2e.HarnessTypes
open Wanxiangshu.Shell.ContextBudgetLimitResolver
open Wanxiangshu.Shell.ContextBudgetUsageCodec

module Dyn = Wanxiangshu.Shell.Dyn

let private createEmpty () = createObj []

let private pad1024 = String.replicate 1024 "x"

let private todowriteArgs (todos: obj array) : obj =
    createObj
        [ "ahaMoments", box pad1024
          "changesAndReasons", box pad1024
          "gotchas", box pad1024
          "lessonsAndConventions", box pad1024
          "plan", box pad1024
          "todos", box todos
          "select_methodology", box [| "first_principles" |] ]

let private todoItem (content: string) (status: string) : obj =
    createObj [ "content", box content; "status", box status; "priority", box "high" ]

/// OpenAI body format: messages[].content = string | [{type,text}].
let private bodyHasNudge (body: obj) : bool =
    if (JS.JSON.stringify (body)).Contains "context is about to be suspended" then
        true
    else
        let messages = Dyn.get body "messages"

        if Dyn.isNullish messages || not (Dyn.isArray messages) then
            false
        else
            unbox<obj array> messages
            |> Array.exists (fun m ->
                let content = Dyn.get m "content"

                if Dyn.isNullish content then
                    false
                elif Dyn.typeIs content "string" then
                    (string content).Contains "context is about to be suspended"
                elif Dyn.isArray content then
                    unbox<obj array> content
                    |> Array.exists (fun p -> (Dyn.str p "text").Contains "context is about to be suspended")
                else
                    false)

let private anyLlmBodyHasNudge (harness: Harness) : bool =
    harness.mockLLM.calls
    |> Seq.cast<obj>
    |> Seq.exists (fun call -> bodyHasNudge (Dyn.get call "body"))

let private providerHasModelLimit (payload: obj) : bool =
    let data = Dyn.get payload "data"
    let providers = Dyn.get data "all"

    if Dyn.isNullish providers || not (Dyn.isArray providers) then
        false
    else
        unbox<obj array> providers
        |> Array.exists (fun provider ->
            if Dyn.str provider "id" <> "test" then
                false
            else
                let models = Dyn.get provider "models"

                let model =
                    if Dyn.isNullish models then
                        null
                    else
                        Dyn.get models "test-model"

                let limit = if Dyn.isNullish model then null else Dyn.get model "limit"
                not (Dyn.isNullish limit) && Dyn.str limit "input" = "20000")

let private sessionInputTokens (payload: obj) : int option =
    let data = Dyn.get payload "data"
    let tokens = Dyn.get data "tokens"
    let input = Dyn.get tokens "input"

    if Dyn.isNullish input || not (Dyn.typeIs input "number") then
        None
    else
        Some(int (unbox<float> input))

/// Real-link e2e: opencode serve + wanxiangshu plugin + mock-llm.
/// Event-driven: sendPrompt blocks until [DONE]; waitForNdjson is
/// event-driven with 1000ms fail-fast. Every short await races 1s.
let run
    (_harness: obj)
    (chk: string -> bool -> unit)
    (startHarnessFn: obj -> JS.Promise<obj>)
    (createEmptyFn: unit -> obj)
    : JS.Promise<int> =
    promise {
        let opts =
            createObj [ "plugin", box true; "sessionId", box "sess-cb"; "contextLimit", box 20000 ]

        let! cbHarnessObj = startHarnessFn opts
        let cbHarness = unbox<Harness> cbHarnessObj

        let! res = withTimeout (cbHarness.createSession (createObj []) createEmptyFn)
        let data = unbox<obj> res
        chk "cb.sessionCreate" (data?ok = true)
        let sessionID = string (data?data?data?id)
        chk "cb.sessionIdResolved" (sessionID <> "")

        // --- Round 1: LLM calls official todowrite with full 5-report
        // payload -> ProgressObserver appends work_backlog_committed.
        cbHarness.mockLLM.expectTool "todowrite" (todowriteArgs [| todoItem "phase-reset repro" "in_progress" |])

        let! _prompt1 = withTimeout (cbHarness.sendPrompt sessionID "commit the first todo" createEmptyFn)

        let! ndjsonOk = withTimeout (cbHarness.waitForNdjson 1 1000)
        chk "cb.round1.ndjsonWritten" ndjsonOk

        let! ndjson1 = withTimeout (cbHarness.readNdjson ())
        chk "cb.round1.eventKindPresent" (ndjson1.Contains "work_backlog_committed")

        let! round1Session = withTimeout (cbHarness.getSession sessionID createEmptyFn)
        let round1Input = sessionInputTokens (unbox<obj> round1Session)
        chk "cb.round1.realTokenUsage" (round1Input |> Option.exists (fun tokens -> tokens > 0))
        chk "cb.round1.tokenUsageAboveBudgetThreshold" (round1Input |> Option.exists (fun tokens -> tokens > 7500))

        let! sessionResponse = withTimeout (cbHarness.getSession sessionID createEmptyFn)
        let sessionBody = Dyn.get (unbox<obj> sessionResponse) "data"
        let sessionModel = Dyn.get sessionBody "model"
        equal "cb.sessionModelId" "test-model" (Dyn.str sessionModel "id")
        equal "cb.sessionProviderId" "test" (Dyn.str sessionModel "providerID")

        let! resolvedLimit =
            withTimeout (resolveMaxInputTokens [ cbHarness.contextBudgetClient () ] sessionID cbHarness.workDir)

        equal "cb.realContextInputLimitResolved" 20000 resolvedLimit

        let! directLimit =
            withTimeout (
                tryGetModelLimitFromProviderListDetailed
                    (cbHarness.contextBudgetClient ())
                    "test-model"
                    "test"
                    cbHarness.workDir
            )

        equal "cb.providerModelLimitResolved" (Some(InputLimit 20000)) directLimit

        let! beforeRound2Session = withTimeout (cbHarness.getSession sessionID createEmptyFn)
        let beforeRound2Input = sessionInputTokens (unbox<obj> beforeRound2Session)
        chk "cb.round2.realTokenUsageAboveThreshold" (beforeRound2Input |> Option.exists (fun tokens -> tokens > 7500))

        let usageReader =
            tryGetRealContextUsage (cbHarness.contextBudgetClient ()) sessionID cbHarness.workDir

        let! resolvedUsage =
            match usageReader with
            | Some reader -> withTimeout (reader [||])
            | None -> Promise.lift None

        chk "cb.round2.contextUsageReaderAboveThreshold" (resolvedUsage |> Option.exists (fun tokens -> tokens > 7500))

        // --- Round 2: LLM plain text. Only change = new backlog entry.
        // rebuildPhaseState triggers phase reset. Bug: phaseBaseTokens was
        // reset to ~current total -> F threshold -> ~100% -> nudge vanishes.
        // Fix: P inherited from old phase -> threshold stays low -> nudge fires.
        cbHarness.mockLLM.expectText "ok"
        let! _prompt2 = withTimeout (cbHarness.sendPrompt sessionID "continue" createEmptyFn)

        let! provRes = withTimeout (cbHarness.listProviders ())
        chk "cb.realProviderModelLimit" (providerHasModelLimit (unbox<obj> provRes))

        chk "cb.round2.nudgeInjectedAfterPhaseReset" (anyLlmBodyHasNudge cbHarness)

        do! withTimeout (cbHarness.dispose ())
        return 0
    }
