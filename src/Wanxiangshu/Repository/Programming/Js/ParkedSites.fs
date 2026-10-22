namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core
open Fable.Core.JsInterop

open System.Threading.Tasks
open Wanxiangshu.Repository.Programming.Js

/// Module-level parked-site table (PLAN S2 step 3, algorithms C/D/E/F/G).
/// The table is a process-live object: it resets on restart, never touches
/// durable storage, and consumes each handle exactly once with no tombstone.
module JsParkedSites =

    /// A parked site: the in-flight sandbox run handle (whose closure chain
    /// holds the vm context and whose DeadlineRef is resettable), the read
    /// snapshots for write-before checks, the as-is ledger the resumed
    /// program keeps firing into (settled when the resumed run finishes),
    /// the session owner, and the Parked state.
    type JsParkedSite =
        { Handle: string
          RunHandle: Wanxiangshu.Process.JsSandbox.JsRunHandle
          ReadSnapshots: ResizeArray<JsReadSnapshot>
          Ledger: JsAsIsLedger
          SessionId: string
          ModelSource: string }

    /// Concurrency ceiling for parked sites (PLAN S2 step 3: default 8).
    let concurrencyLimit = 8

    // DSL-MUTABLE: resource — process-live parked-site table. Reset on
    // restart by construction (module state); handles are consumed once.
    let private table = System.Collections.Generic.Dictionary<string, JsParkedSite>()
    let private expiry = System.Collections.Generic.Dictionary<string, int64>()
    let private ttlMs = 30L * 60L * 1000L
    let private timerLock = obj ()
    let mutable private sweeper: obj option = None

    let private nowMs () =
        System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    /// The sweeper belongs to the parked sites and ends with the last one.
    let private stopSweeperIfEmpty () =
        match sweeper with
        | Some timer when table.Count = 0 ->
            emitJsExpr timer "clearInterval($0)" |> ignore
            sweeper <- None
        | _ -> ()

    let private abandonExpired () =
        lock timerLock (fun () ->
            let expired =
                expiry
                |> Seq.filter (fun kv -> kv.Value <= nowMs ())
                |> Seq.map (fun kv -> kv.Key)
                |> Seq.toList

            for handle in expired do
                match table.TryGetValue handle with
                | true, site ->
                    Wanxiangshu.Process.JsSandbox.abandon site.RunHandle.ParkState |> ignore
                    table.Remove handle |> ignore
                    expiry.Remove handle |> ignore
                | _ -> expiry.Remove handle |> ignore

            stopSweeperIfEmpty ())

    let private ensureSweeper () =
        if sweeper.IsNone then
            let callback = fun () -> abandonExpired ()
            sweeper <- Some(emitJsExpr callback "(function(cb) { const t = setInterval(cb, 60000); if (t && typeof t.unref === 'function') t.unref(); return t; })($0)")

    /// Register a parked site under its handle. Refuses with CONCURRENCY_LIMIT
    /// when the table already holds `concurrencyLimit` sites (no queue).
    /// The handle is the name the caller gave this invocation. An empty name
    /// or a name already parked is refused; there is no park-N generator.
    let register (site: JsParkedSite) : Result<string, JsFailure> =
        if System.String.IsNullOrWhiteSpace site.Handle then
            Error(JsFailure.InvalidArgument "name is required")
        elif table.ContainsKey site.Handle then
            Error(JsFailure.InvalidArgument("name is already parked: " + site.Handle))
        elif table.Count >= concurrencyLimit then
            Error JsFailure.ConcurrencyLimit
        else
            table.[site.Handle] <- site
            expiry.[site.Handle] <- nowMs () + ttlMs
            ensureSweeper ()
            Ok site.Handle

    /// Look up a parked site by handle.
    let tryFind (handle: string) : JsParkedSite option =
        abandonExpired ()

        match table.TryGetValue handle with
        | true, site -> Some site
        | false, _ -> None

    /// Remove a parked site (one-shot consumption; no tombstone).
    let remove (handle: string) : unit =
        table.Remove handle |> ignore
        expiry.Remove handle |> ignore
        stopSweeperIfEmpty ()

    /// Algorithm G1/G2: all unresolved parked handles (deliver gate + PairHint).
    let parkedHandles () : string list =
        abandonExpired ()
        table.Keys |> Seq.toList |> List.sort

    /// Algorithm G3: handles currently parked by the given session. The
    /// session-close caller enumerates them before reapSession consumes the
    /// sites, so the durable Reaped audit can name every handle.
    let handlesForSession (sessionId: string) : string list =
        abandonExpired ()

        table.Values
        |> Seq.filter (fun site -> site.SessionId = sessionId)
        |> Seq.map (fun site -> site.Handle)
        |> Seq.toList
        |> List.sort

    [<RequireQualifiedAccess>]
    type ParkResourceClassification =
        | ProvenSideEffectFreePause of reason: string
        | PendingUserAsk of question: string
        | ActiveChildCommand of detail: string
        | AmbiguitySearch of needle: string
        | UnclassifiedDeclaredPause of payloadKind: string

    [<Emit("$0 !== null && $0 !== undefined && typeof $0 === 'object' && ('question' in $0)")>]
    let private isAskPayload (payload: obj) : bool = jsNative

    [<Emit("String(($0 && $0.question) || '')")>]
    let private askQuestionText (payload: obj) : string = jsNative

    [<Emit("$0 !== null && $0 !== undefined && typeof $0 === 'object' && Boolean($0.__jsYield)")>]
    let private isYieldPayload (payload: obj) : bool = jsNative

    [<Emit("$0 !== null && $0 !== undefined && typeof $0 === 'object' && ('needle' in $0) && ('candidates' in $0)")>]
    let private isAmbiguityPayload (payload: obj) : bool = jsNative

    [<Emit("String(($0 && $0.needle) || '')")>]
    let private ambiguityNeedleText (payload: obj) : string = jsNative

    [<Emit("$0 !== null && $0 !== undefined && typeof $0 === 'object' && Boolean($0.__jsKnownNoEffect)")>]
    let private isKnownNoEffectPayload (payload: obj) : bool = jsNative

    [<Emit("($0 === null || $0 === undefined) ? 'empty' : typeof $0")>]
    let private payloadKindText (payload: obj) : string = jsNative

    /// Classify a parked site by its real resource ownership.
    let classifySite (site: JsParkedSite) : ParkResourceClassification =
        let payload = site.RunHandle.ParkState.ParkPayload
        if isKnownNoEffectPayload payload then
            // EXPLICIT ALLOW-LIST ONLY: Proven side-effect-free observation checkpoint.
            ParkResourceClassification.ProvenSideEffectFreePause "runtime-verified side-effect-free pause"
        elif isAskPayload payload then
            ParkResourceClassification.PendingUserAsk (askQuestionText payload)
        elif isYieldPayload payload then
            ParkResourceClassification.ActiveChildCommand "command execution yield"
        elif isAmbiguityPayload payload then
            // Ambiguity cannot be statically proven side-effect-free in general.
            ParkResourceClassification.AmbiguitySearch (ambiguityNeedleText payload)
        else
            // Default: any other declared pause is blocked to avoid silently discarding caller's pause.
            ParkResourceClassification.UnclassifiedDeclaredPause (payloadKindText payload)

    let isDeliveryBlocker (site: JsParkedSite) : bool =
        match classifySite site with
        | ParkResourceClassification.ProvenSideEffectFreePause _ -> false
        | _ -> true

    /// Auto-settle side-effect-free parked sites for this session: abandon their
    /// continuation (triggering native finally blocks) and remove their handles.
    /// Returns the list of auto-settled handle names. Uncleared blockers remain.
    let deliveryBlockersWithChoicesForSession (sessionId: string) : (string * string) list =
        abandonExpired ()
        table.Values
        |> Seq.filter (fun site -> site.SessionId = sessionId)
        |> Seq.choose (fun site ->
            match classifySite site with
            | ParkResourceClassification.PendingUserAsk question ->
                Some(site.Handle, sprintf "handle '%s' is waiting for user answer to question '%s'; answer with unpark({ handle, value }) or cancel with abandon({ handle })" site.Handle question)
            | ParkResourceClassification.ActiveChildCommand detail ->
                Some(site.Handle, sprintf "handle '%s' holds an active background command (%s); wait for completion, send signal, or cancel with abandon({ handle })" site.Handle detail)
            | ParkResourceClassification.AmbiguitySearch needle ->
                Some(site.Handle, sprintf "handle '%s' is paused on where ambiguity search for '%s'; resolve with unpark({ handle, value }) or cancel with abandon({ handle })" site.Handle needle)
            | ParkResourceClassification.UnclassifiedDeclaredPause kind ->
                Some(site.Handle, sprintf "handle '%s' declared an unresolved pause of kind '%s'; resume with unpark({ handle, value }) or cancel with abandon({ handle })" site.Handle kind)
            | ParkResourceClassification.ProvenSideEffectFreePause _ ->
                None)
        |> Seq.toList
        |> List.sortBy fst

    let deliveryBlockersForSession (sessionId: string) : string list =
        deliveryBlockersWithChoicesForSession sessionId |> List.map fst

    let autoSettleForSession (sessionId: string) : string list =
        abandonExpired ()
        let settleable =
            table.Values
            |> Seq.filter (fun site ->
                site.SessionId = sessionId &&
                match classifySite site with
                | ParkResourceClassification.ProvenSideEffectFreePause _ -> true
                | _ -> false)
            |> Seq.toList

        let settled = ResizeArray<string>()
        for site in settleable do
            Wanxiangshu.Process.JsSandbox.abandon site.RunHandle.ParkState
            remove site.Handle
            settled.Add site.Handle

        settled |> Seq.toList

    /// Algorithm F/G3: abandon every parked site owned by the session. The
    /// ABANDONED rejection unwinds each program's stack so native try/finally
    /// runs; the site is then removed. Durable Reaped audit is the caller's
    /// responsibility (PluginSessionScope records it).
    let reapSession (sessionId: string) : Task =
        task {
            let owned =
                table.Values
                |> Seq.filter (fun site -> site.SessionId = sessionId)
                |> Seq.toList

            for site in owned do
                Wanxiangshu.Process.JsSandbox.abandon site.RunHandle.ParkState
                remove site.Handle

            JsDeliveredValues.clear sessionId
        }

    /// Test hook: clear the whole table.
    let clearAll () : unit =
        table.Clear()
        expiry.Clear()
        stopSweeperIfEmpty ()
