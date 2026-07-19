module Wanxiangshu.Hosts.Omp.ChildCleanup

open Fable.Core

/// Review / executor child sessions MUST be torn down on
/// every code path, not only the happy path.  The previous
/// implementation duplicated the abort+dispose pattern inline in
/// three places, which made it easy to miss a path and leak the
/// child.  This module is the single place that performs the unified
/// teardown.
///
/// CleanupChildSession tolerates individual failures: every operation
/// is wrapped in its own try/with so a single failure (e.g. the host
/// already aborted the session internally) does not prevent the others
/// from running.  Callers MUST invoke it from a finally block or as
/// the last statement of every branch.
let CleanupChildSession (childSession: obj) (dispose: (unit -> unit) option) : unit =
    let hasAbort =
        try
            let abortFn = Wanxiangshu.Runtime.Dyn.get childSession "abort"
            not (Wanxiangshu.Runtime.Dyn.isNullish abortFn)
        with _ ->
            false

    if hasAbort then
        try
            Wanxiangshu.Runtime.Dyn.callMethod0 childSession "abort" |> ignore
        with _ ->
            ()

    match dispose with
    | Some d ->
        try
            d ()
        with _ ->
            ()
    | None -> ()
