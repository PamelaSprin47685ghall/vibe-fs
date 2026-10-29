namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Context.Scope
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Persistence.Journal

/// Host-side Scope annotation gate. Schema is injected onto every ToolSpec;
/// semantics live in wanxiangli/scope-retention and common law «Scope».
/// scope.kind=resetScope on any tool drives ScopeReset.commit (no standalone tool).
module ScopeAnnotationGate =

    [<Import("scopeRetention", "wanxiangli/scope-retention")>]
    let private scopeRetention: obj = jsNative

    [<Emit("$0 == null")>]
    let private isNullish (_value: obj) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private field (_value: obj) (_name: string) : obj = jsNative

    // DSL-MUTABLE: resource — the provider run whose real annotation is a
    // resetScope, per session. The reset commits asynchronously on a new epoch;
    // a sibling meToo in the same run must not depend on whether it arrives
    // before or after that commit.
    let private resetRuns =
        System.Collections.Generic.Dictionary<string, string>()

    // ── Held meToo (common law «Scope»: arrival order inside a turn does not
    // matter; an all-meToo turn is refused). A meToo that arrives before its
    // turn's real annotation is not run yet: it waits, unexecuted, until the
    // real annotation of the same provider run is accepted (then it runs) or
    // the run goes quiet with none (then the whole turn is refused, nothing
    // spent). Every gate arrival in the run restarts the quiet window, so a
    // real sibling still streaming its arguments is waited for.

    [<Emit("(() => { let r; const p = new Promise(x => { r = x; }); return [p, r]; })()")>]
    let private deferred () : obj = jsNative

    [<Emit("$0[$1]")>]
    let private at (_value: obj) (_index: int) : obj = jsNative

    [<Emit("$0($1)")>]
    let private invoke (_fn: obj) (_arg: obj) : unit = jsNative

    [<Emit("setTimeout($0, $1)")>]
    let private startTimer (_callback: unit -> unit) (_ms: int) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let private stopTimer (_handle: obj) : unit = jsNative

    [<Emit("(typeof process !== 'undefined' && process.env ? process.env.WANXIANGSHU_METOO_HOLD_MS : undefined)")>]
    let private holdWindowEnv () : obj = jsNative

    /// Quiet window after the run's last gate arrival before an all-meToo
    /// turn is settled as refused.
    let private defaultHoldWindowMs = 30000

    type private HeldTurn =
        { mutable Waiters: obj list
          mutable Timer: obj option }

    // DSL-MUTABLE: resource — meToo calls held per session+run until the turn is decided.
    let private heldTurns =
        System.Collections.Generic.Dictionary<string, HeldTurn>()

    let private heldKey (sessionId: string) (runId: string) =
        string sessionId.Length + ":" + sessionId + ":" + runId

    /// Resolve every held meToo of the run: "" runs it, any other text is the refusal.
    let private resolveHeld (key: string) (verdict: string) =
        match heldTurns.TryGetValue key with
        | true, held ->
            heldTurns.Remove key |> ignore
            held.Timer |> Option.iter stopTimer
            for resolve in List.rev held.Waiters do
                invoke resolve (box verdict)
        | _ -> ()

    let private asString (value: obj) : string =
        if isNullish value then "" else string value

    let private asInt (value: obj) (fallback: int) : int =
        if isNullish value then fallback
        else
            match System.Int32.TryParse(string value) with
            | true, n -> n
            | _ -> fallback

    let private holdWindowMs () =
        let raw = holdWindowEnv ()
        if isNullish raw then defaultHoldWindowMs
        else
            match System.Int32.TryParse(string raw) with
            | true, n when n >= 0 -> n
            | _ -> defaultHoldWindowMs

    let private allMeTooRefusal =
        "meToo requires exactly one real scope annotation (resetScope|keepLast|removeLast) in the same turn; this turn carried only meToo and is refused"

    /// The run went quiet without a real annotation: settle it in retention
    /// and refuse every held meToo.
    let private settleHeld (sessionId: string) (runId: string) =
        let key = heldKey sessionId runId
        if heldTurns.ContainsKey key then
            let refused: string array = scopeRetention?settle (sessionId, runId)
            ignore refused
            resolveHeld key allMeTooRefusal

    /// Any gate arrival in a run that holds meToo restarts its quiet window.
    let private touchHeld (sessionId: string) (runId: string) =
        match heldTurns.TryGetValue(heldKey sessionId runId) with
        | true, held ->
            held.Timer |> Option.iter stopTimer
            held.Timer <- Some(startTimer (fun () -> settleHeld sessionId runId) (holdWindowMs ()))
        | _ -> ()

    /// Hold one provisional meToo; the task completes with "" (run it) or the refusal.
    let private holdMeToo (sessionId: string) (runId: string) : Task<string> =
        let key = heldKey sessionId runId
        let d = deferred ()
        let held =
            match heldTurns.TryGetValue key with
            | true, held -> held
            | _ ->
                let held = { Waiters = []; Timer = None }
                heldTurns[key] <- held
                held
        held.Waiters <- at d 1 :: held.Waiters
        touchHeld sessionId runId
        unbox<Task<string>> (at d 0)

    /// Apply the required `scope` argument. Returns Error prose on refusal.
    /// For resetScope, validation-only here — durable archive is ScopeReset.commit.
    let apply
        (toolName: string)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        (epochOf: HostToolContext -> string)
        : Result<obj, string> =
        let scope = field args.Raw "scope"

        if isNullish scope then
            Error "scope annotation required on every tool call (see common law «Scope»)"
        else
            match ctx.ToolCallId with
            | None -> Error "scope annotation requires tool call id"
            | Some call ->
                let callText = ToolCallId.value call
                let runText =
                    match ctx.ProviderRunId with
                    | Some run -> ProviderRunIdentity.value run
                    | None -> callText

                let beatId = runText

                let epoch =
                    let e = epochOf ctx
                    if String.IsNullOrWhiteSpace e then "0" else e

                touchHeld ctx.SessionId runText
                let kind = asString (field scope "kind")
                // resetScope: validate fields without mutating retention on the
                // old epoch — ScopeReset.commit seeds the new epoch.
                if kind = "resetScope" then
                    let goal = asString (field scope "newScopeGoal")
                    let budget = asInt (field scope "newScopeRetentionBudget") 0
                    let oldV = asString (field scope "oldScopeVerboseVerbatim")
                    if String.IsNullOrWhiteSpace goal then
                        Error "resetScope requires newScopeGoal"
                    elif budget < 1 then
                        Error "resetScope requires newScopeRetentionBudget in 1..48"
                    else
                        resetRuns[ctx.SessionId] <- runText
                        // The reset is this turn's real annotation: held siblings run.
                        resolveHeld (heldKey ctx.SessionId runText) ""
                        Ok(
                            createObj
                                [ "ok" ==> true
                                  "reanchor" ==> true
                                  "remaining" ==> budget
                                  "goal" ==> goal.Trim()
                                  "oldVerbatim" ==> oldV.Trim() ]
                        )
                elif kind = "meToo"
                     && (match resetRuns.TryGetValue ctx.SessionId with
                         | true, run -> run = runText
                         | _ -> false) then
                    Ok(createObj [ "ok" ==> true; "kind" ==> "meToo"; "reanchor" ==> false ])
                else
                    let receipt: obj =
                        scopeRetention?apply (
                            ctx.SessionId,
                            epoch,
                            toolName,
                            runText,
                            callText,
                            beatId,
                            scope
                        )

                    if isNullish receipt || not (unbox<bool> receipt?ok) then
                        let reason =
                            if isNullish receipt then
                                "scope annotation refused"
                            else
                                string receipt?reason

                        Error reason
                    else
                        if unbox<bool> receipt?decided = true then
                            resolveHeld (heldKey ctx.SessionId runText) ""

                        let rem = receipt?remaining

                        if not (isNullish rem) then
                            try
                                Surface.setRetentionBudget ctx.SessionId (unbox<int> rem)
                            with _ ->
                                ()

                        Ok receipt

    /// Tools that clear a consultation gate (first negotiate, first
    /// askPreviousScope after a reset). A resetScope annotation on one of them
    /// commits after the tool ran: the reset is gated on exactly the call that
    /// carries it, so committing first would refuse the unlocking call itself.
    let private unlocksReset (toolName: string) =
        toolName = "negotiate" || toolName = "askPreviousScope" || toolName = "askPrevious"

    /// The reset decided this turn on the new epoch: sibling calls may say
    /// meToo, and the new epoch's retention journal starts with its ScopeReset.
    let private decideResetTurn
        (toolName: string)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        (epochOf: HostToolContext -> string)
        =
        match ctx.ToolCallId with
        | None -> ()
        | Some call ->
            let callText = ToolCallId.value call
            let runText =
                match ctx.ProviderRunId with
                | Some run -> ProviderRunIdentity.value run
                | None -> callText
            let epoch =
                let e = epochOf ctx
                if String.IsNullOrWhiteSpace e then "0" else e
            scopeRetention?apply (ctx.SessionId, epoch, toolName, runText, callText, runText, field args.Raw "scope")
            |> ignore

    let private commitReset
        (toolName: string)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        (epochOf: HostToolContext -> string)
        (journalOpt: AgentJournal option)
        (snapshotPort: ISessionSnapshotPort option)
        (receipt: obj)
        : Task<Result<unit, string>> =
        task {
            let replay = ScopeReset.alreadyCommitted journalOpt ctx
            let goal = asString receipt?goal
            let oldV = asString receipt?oldVerbatim
            let budget = asInt receipt?remaining 0
            let! committed = ScopeReset.commit journalOpt snapshotPort ctx goal oldV budget
            match committed with
            | Error reason ->
                // A refused reset decided nothing; siblings fall back to the ordinary rule.
                resetRuns.Remove ctx.SessionId |> ignore
                return Error reason
            | Ok _ ->
                if not replay then decideResetTurn toolName args ctx epochOf
                return Ok()
        }

    let wrap
        (toolName: string)
        (epochOf: HostToolContext -> string)
        (journalOpt: AgentJournal option)
        (snapshotPort: ISessionSnapshotPort option)
        (inner: HostToolArguments -> HostToolContext -> Task<string>)
        : HostToolArguments -> HostToolContext -> Task<string> =
        fun args ctx ->
            task {
                match apply toolName args ctx epochOf with
                | Error reason -> return reason
                | Ok receipt ->
                    if unbox<bool> (receipt?reanchor) = true then
                        if unlocksReset toolName then
                            let! result = inner args ctx
                            let! committed = commitReset toolName args ctx epochOf journalOpt snapshotPort receipt
                            match committed with
                            | Ok() -> return result
                            | Error reason -> return result + "\n\n" + reason
                        else
                            let! committed = commitReset toolName args ctx epochOf journalOpt snapshotPort receipt
                            match committed with
                            | Error reason -> return reason
                            | Ok() -> return! inner args ctx
                    elif unbox<bool> (receipt?provisional) = true then
                        let runText =
                            match ctx.ProviderRunId, ctx.ToolCallId with
                            | Some run, _ -> ProviderRunIdentity.value run
                            | None, Some call -> ToolCallId.value call
                            | None, None -> ""
                        let! verdict = holdMeToo ctx.SessionId runText
                        if String.IsNullOrEmpty verdict then
                            return! inner args ctx
                        else
                            return verdict
                    else
                        return! inner args ctx
            }

    let injectArgument (factory: HostToolFactory) (spec: ToolSpec) : ToolSpec =
        let already = spec.Arguments |> List.exists (fun (name, _) -> name = "scope")

        if already then
            spec
        else
            { spec with
                Arguments = spec.Arguments @ [ "scope", ToolHostCodec.scopeAnnotationSchema factory ] }
