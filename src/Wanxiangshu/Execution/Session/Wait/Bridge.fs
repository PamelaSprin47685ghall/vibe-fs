namespace Wanxiangshu.Execution.Session.Wait

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// Scheme B diagnostic file bridge. Not Journal. Business code must not read it.
module CausalWaitBridge =

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string, options: obj) : unit = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSync (path: string, data: string, encoding: string) : unit = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("appendFileSync", "node:fs")>]
    let private appendFileSync (path: string, data: string, encoding: string) : unit = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Import("pid", "node:process")>]
    let private processId: int = jsNative

    [<Emit("JSON.stringify($0, null, 2)")>]
    let private stringify (value: obj) : string = jsNative

    let private existingExclude (excludePath: string) : string =
        if existsSync excludePath then
            readFileSync (excludePath, "utf8")
        else
            ""

    let private linePrefix (existing: string) : string =
        if existing.Length = 0 || existing.EndsWith("\n") then
            ""
        else
            "\n"

    let private appendDiagnosticMarker (excludePath: string) (existing: string) : unit =
        if not (existing.Contains ".wanxiangshu/") then
            let prefix = linePrefix existing

            appendFileSync (
                excludePath,
                prefix + "# wanxiangshu diagnostic bridge (non-authoritative)\n.wanxiangshu/\n",
                "utf8"
            )

    /// Keep diagnostic files out of `git status` / IsDirty. Best-effort only.
    let private ensureDiagnosticsGitExcluded (workspace: string) : unit =
        try
            let gitInfo = pathJoin (pathJoin (workspace, ".git"), "info")
            mkdirSync (gitInfo, {| recursive = true |})
            let excludePath = pathJoin (gitInfo, "exclude")
            appendDiagnosticMarker excludePath (existingExclude excludePath)
        with _ ->
            ()

    let private pairs (xs: (string * string) list) : obj =
        box (
            xs
            |> List.map (fun (k, v) -> createObj [ "k", box k; "v", box v ])
            |> Array.ofList
        )

    let private ownerObj (owner: CausalOwnerRef) : obj =
        createObj [ "kind", box owner.Kind; "identity", pairs owner.Identity ]

    let private producerObj (producer: CausalProducerRef) : obj =
        match producer with
        | WorkflowProducer owner -> createObj [ "tag", box "workflow"; "owner", ownerObj owner ]
        | ExternalProducer(kind, identity) ->
            createObj [ "tag", box "external"; "kind", box kind; "identity", pairs identity ]

    let private escapeObj (escape: WaitEscape) : obj =
        match escape with
        | DeadlineAt at -> createObj [ "tag", box "deadlineAt"; "at", box (at.ToUniversalTime().ToString("o")) ]
        | CancelledBy owner -> createObj [ "tag", box "cancelledBy"; "owner", ownerObj owner ]
        | ProcessLifetime -> createObj [ "tag", box "processLifetime" ]
        | SessionLifetime -> createObj [ "tag", box "sessionLifetime" ]
        | OpenEndedExternal -> createObj [ "tag", box "openEndedExternal" ]

    let private waitObj (wait: DiagnosticWait) : obj =
        createObj
            [ "waitKind", box wait.WaitKind
              "owner", ownerObj wait.Owner
              "subject", pairs wait.Subject
              "producer", producerObj wait.Producer
              "escapes", box (wait.Escapes |> List.map escapeObj |> Array.ofList)
              "source", box wait.Source ]

    let private exitName (exit: DiagnosticWaitExit) : string =
        match exit with
        | WaitResolved -> "resolved"
        | WaitFailed -> "failed"
        | WaitCancelled -> "cancelled"
        | WaitTimedOut -> "timedOut"
        | WaitDisposed -> "disposed"

    let private transitionObj (transition: WaitTransition) : obj =
        let kind =
            match transition.Kind with
            | WaitTransitionKind.Entered -> "entered"
            | WaitTransitionKind.Left -> "left"

        createObj
            [ "sequence", box (string transition.Sequence)
              "kind", box kind
              "wait", waitObj transition.Wait
              "exit",
              box (
                  match transition.Exit with
                  | Some exit -> exitName exit :> obj
                  | None -> null
              ) ]

    let private frontierKindName (kind: CausalFrontierKind) : string =
        match kind with
        | ExternalProducerFrontier -> "ExternalProducerFrontier"
        | BrokenCausalEdge -> "BrokenCausalEdge"
        | ProducerRunningWithoutWait -> "ProducerRunningWithoutWait"
        | CausalWaitCycle -> "CausalWaitCycle"
        | Empty -> "Empty"

    let private frontierNodeObj (node: CausalFrontierNode) : obj =
        createObj
            [ "owner", ownerObj node.Owner
              "waitKind",
              box (
                  match node.Wait with
                  | Some wait -> wait.WaitKind :> obj
                  | None -> null
              ) ]

    let private frontierObj (frontier: CausalFrontier) : obj =
        createObj
            [ "kind", box (frontierKindName frontier.Kind)
              "detail", box frontier.Detail
              "chain", box (frontier.Chain |> List.map frontierNodeObj |> Array.ofList)
              "frontierProducer",
              box (
                  match frontier.FrontierProducer with
                  | Some producer -> producerObj producer
                  | None -> null
              )
              "cycle", box (frontier.Cycle |> List.map ownerObj |> Array.ofList) ]

    /// Plain JS object for diagnostics consumers. Never authoritative.
    let toPlainObject (reader: IWaitSnapshotReader) : obj =
        let snapshot = reader.Snapshot()
        let frontiers = CausalFrontier.ofSnapshot snapshot

        createObj
            [ "pid", box processId
              "sequence", box (string snapshot.Sequence)
              "active", box (snapshot.Active |> List.map waitObj |> Array.ofList)
              "history", box (snapshot.History |> List.map transitionObj |> Array.ofList)
              "frontiers", box (frontiers |> List.map frontierObj |> Array.ofList) ]

    let private writeSnapshotUnsafe (workspace: string) (reader: IWaitSnapshotReader) =
        try
            ensureDiagnosticsGitExcluded workspace
            let diagnosticsDir = pathJoin (pathJoin (workspace, ".wanxiangshu"), "diagnostics")
            mkdirSync (diagnosticsDir, {| recursive = true |})
            let filePath = pathJoin (diagnosticsDir, "causal-waits.json")
            writeFileSync (filePath, stringify (toPlainObject reader), "utf8")
        with _ ->
            ()

    /// Best-effort overwrite of `<workspace>/.wanxiangshu/diagnostics/causal-waits.json`.
    let writeSnapshot (workspace: string) (reader: IWaitSnapshotReader) : unit =
        if String.IsNullOrWhiteSpace workspace then
            ()
        else
            writeSnapshotUnsafe workspace reader
