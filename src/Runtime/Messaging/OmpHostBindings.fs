module Wanxiangshu.Runtime.OmpHostBindings

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

/// OMP host wire contract (SPEC §4.5 / Phase7).
/// Status: Verified = encoded in OmpHostContract* tests with mocks.
/// Unverified = fail-closed; business logic MUST NOT depend on it.
///
/// | Contract              | Status       | Rule |
/// |-----------------------|--------------|------|
/// | prompt return moment  | Unverified   | resolve ≠ ordered accept; never emit OrderedTurnMarkerObserved from resolve alone |
/// | prompt return shape   | Partial      | try extract id/messageId/data.id; absent → HostAcceptanceUnknown |
/// | idle before resolve   | Unverified   | may race; summarizer anchors by entry baseline, not bare waitForIdle |
/// | message persistence   | Partial      | continuationId/continuationID on info when present |
/// | abort idempotency     | Partial      | single physical path: session.abort preferred, else pi.sessionAbort; never both |
/// | model omit vs ""      | Verified     | omit key when no model; empty string forbidden |
/// | event order           | Unverified   | fail closed on missing correlation ids |
///
/// Fail-closed defaults: no fabricated ordered markers; no dual abort; no empty model string.

let getCreateAgentSession (pi: obj) : obj =
    Dyn.get (Dyn.get pi "pi") "createAgentSession"

let createSessionManager (sessionManagerType: obj) (cwd: string) : obj =
    Dyn.call1 (Dyn.get sessionManagerType "create") (box cwd)

let tryExtractMessageId (response: obj) : string option =
    if Dyn.isNullish response then
        None
    else
        let pick (o: obj) =
            if Dyn.isNullish o then
                None
            else
                let a = Dyn.str o "id"
                let b = if a <> "" then a else Dyn.str o "messageId"
                let c = if b <> "" then b else Dyn.str o "messageID"
                if c <> "" then Some c else None

        match pick response with
        | Some _ as hit -> hit
        | None ->
            let data = Dyn.get response "data"

            match pick data with
            | Some _ as hit -> hit
            | None ->
                let msg = Dyn.get response "message"

                match pick msg with
                | Some _ as hit -> hit
                | None -> pick (Dyn.get data "message")

let formatModelString (providerId: string) (modelId: string) (variant: string option) : string option =
    if providerId = "" || modelId = "" then
        None
    else
        match variant with
        | Some v when v <> "" -> Some(sprintf "%s/%s:%s" providerId modelId v)
        | _ -> Some(sprintf "%s/%s" providerId modelId)

/// OMP host prompt envelope: structured fields { text; model?; continuationID?; agent? }.
/// This is the session.prompt wire payload (not a freeform prompt bag).
let buildSessionPromptPayload
    (text: string)
    (model: string option)
    (continuationId: string option)
    (agent: string option)
    : obj =
    match model, continuationId, agent with
    | Some m, Some c, Some a when m <> "" && c <> "" && a <> "" ->
        box {| text = text; model = m; continuationID = c; agent = a |}
    | Some m, Some c, _ when m <> "" && c <> "" -> box {| text = text; model = m; continuationID = c |}
    | Some m, _, Some a when m <> "" && a <> "" -> box {| text = text; model = m; agent = a |}
    | _, Some c, Some a when c <> "" && a <> "" -> box {| text = text; continuationID = c; agent = a |}
    | Some m, _, _ when m <> "" -> box {| text = text; model = m |}
    | _, Some c, _ when c <> "" -> box {| text = text; continuationID = c |}
    | _, _, Some a when a <> "" -> box {| text = text; agent = a |}
    | _ -> box {| text = text |}

let sessionPrompt (session: obj) (prompt: string) : JS.Promise<obj> =
    unbox<JS.Promise<obj>> (Dyn.callMethod1 session "prompt" (box prompt))

let sessionPromptViaApi (sessionApi: obj) (sessionId: string) (promptPayload: obj) : JS.Promise<obj> =
    let envelope = box {| prompt = promptPayload |}
    let arg = box {| sessionId = sessionId; body = envelope |}
    unbox<JS.Promise<obj>> (sessionApi?sessionPrompt (arg))

let sessionWaitForIdle (session: obj) : JS.Promise<unit> =
    unbox<JS.Promise<unit>> (Dyn.callMethod0 session "waitForIdle")

let sessionAbort (session: obj) : obj = Dyn.get session "abort"

let tryCallSessionAbort (session: obj) : JS.Promise<bool> option =
    try
        let abortFn = Dyn.get session "abort"

        if Dyn.isNullish abortFn || not (Dyn.typeIs abortFn "function") then
            None
        else
            Some(
                unbox<JS.Promise<obj>> (session?abort ())
                |> Promise.map (fun _ -> true)
                |> Promise.catch (fun _ -> false)
            )
    with _ ->
        None

let tryCallPiSessionAbort (pi: obj) (sessionId: string) : JS.Promise<bool> option =
    try
        let sessionApi = Dyn.get pi "session"

        if Dyn.isNullish sessionApi then
            None
        else
            let abortFn = Dyn.get sessionApi "sessionAbort"

            if Dyn.isNullish abortFn || not (Dyn.typeIs abortFn "function") then
                None
            else
                let arg = box {| sessionId = sessionId |}

                Some(
                    unbox<JS.Promise<obj>> (sessionApi?sessionAbort (arg))
                    |> Promise.map (fun _ -> true)
                    |> Promise.catch (fun _ -> false)
                )
    with _ ->
        None

/// Single-path abort: prefer session.abort; else pi.sessionAbort; never both.
let abortOnce (session: obj) (pi: obj) (sessionId: string) : JS.Promise<struct (bool * bool)> =
    promise {
        match tryCallSessionAbort session with
        | Some p ->
            let! ok = p
            return struct (ok, true)
        | None ->
            match tryCallPiSessionAbort pi sessionId with
            | Some p ->
                let! ok = p
                return struct (ok, true)
            | None -> return struct (false, false)
    }

let entryCountOfSession (session: obj) : int =
    let sm = Dyn.get session "sessionManager"

    if Dyn.isNullish sm then
        0
    else
        let getEntries = Dyn.get sm "getEntries"

        let raw =
            if Dyn.typeIs getEntries "function" then
                Dyn.callMethod0 sm "getEntries"
            else
                Dyn.get sm "messages"

        if Dyn.isArray raw then (unbox<obj array> raw).Length else 0

/// Wait until idle settles with transcript growth past baseline (target turn),
/// not an arbitrary pre-existing idle. Caps attempts to avoid livelock.
let waitForIdleAfterBaseline (session: obj) (baseline: int) (maxAttempts: int) : JS.Promise<bool> =
    let rec loop remaining =
        promise {
            do! sessionWaitForIdle session
            let n = entryCountOfSession session

            if n > baseline then
                return true
            elif remaining <= 1 then
                return false
            else
                do! Promise.sleep 5
                return! loop (remaining - 1)
        }

    loop (max maxAttempts 1)
