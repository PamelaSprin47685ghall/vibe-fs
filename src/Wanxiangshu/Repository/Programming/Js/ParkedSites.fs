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

    let private nowMs () = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

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
                | _ ->
                    expiry.Remove handle |> ignore
        )

    let private ensureSweeper () =
        if sweeper.IsNone then
            let callback = fun () -> abandonExpired ()
            sweeper <- Some (emitJsExpr callback "setInterval($0, 60000)")

    /// Register a parked site under its handle. Refuses with CONCURRENCY_LIMIT
    /// when the table already holds `concurrencyLimit` sites (no queue).
    /// The handle is the name the caller gave this invocation. An empty name
    /// or a name already parked is refused; there is no park-N generator.
    let register (site: JsParkedSite) : Result<string, JsFailure> =
        if System.String.IsNullOrWhiteSpace site.Handle then
            Error(JsFailure.InvalidArgument "name is required")
        elif table.ContainsKey site.Handle then
            Error(JsFailure.InvalidArgument ("name is already parked: " + site.Handle))
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
