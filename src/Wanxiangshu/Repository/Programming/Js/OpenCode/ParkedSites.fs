namespace Wanxiangshu.Repository.Programming.Js.OpenCode

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
          SessionId: string }

    /// Concurrency ceiling for parked sites (PLAN S2 step 3: default 8).
    let concurrencyLimit = 8

    // DSL-MUTABLE: resource — process-live parked-site table. Reset on
    // restart by construction (module state); handles are consumed once.
    let private table = System.Collections.Generic.Dictionary<string, JsParkedSite>()
    let private nextHandle = ref 0

    /// Allocate a monotonically increasing process-unique handle (algorithm C2).
    let allocateHandle () : string =
        incr nextHandle
        sprintf "park-%d" !nextHandle

    /// Register a parked site under its handle. Refuses with CONCURRENCY_LIMIT
    /// when the table already holds `concurrencyLimit` sites (no queue).
    let register (site: JsParkedSite) : Result<string, JsFailure> =
        if table.Count >= concurrencyLimit then
            Error JsFailure.ConcurrencyLimit
        else
            table.[site.Handle] <- site
            Ok site.Handle

    /// Look up a parked site by handle.
    let tryFind (handle: string) : JsParkedSite option =
        match table.TryGetValue handle with
        | true, site -> Some site
        | false, _ -> None

    /// Remove a parked site (one-shot consumption; no tombstone).
    let remove (handle: string) : unit = table.Remove handle |> ignore

    /// Algorithm G1/G2: all unresolved parked handles (deliver gate + PairHint).
    let parkedHandles () : string list = table.Keys |> Seq.toList |> List.sort

    /// Algorithm G3: handles currently parked by the given session. The
    /// session-close caller enumerates them before reapSession consumes the
    /// sites, so the durable Reaped audit can name every handle.
    let handlesForSession (sessionId: string) : string list =
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
        }

    /// Test hook: clear the whole table.
    let clearAll () : unit = table.Clear()
