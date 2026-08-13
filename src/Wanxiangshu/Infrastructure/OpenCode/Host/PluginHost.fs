namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Review
open Wanxiangshu.Session

module PluginHost =

    [<Import("pid", "node:process")>]
    let processId: int = jsNative

    let workspaceDirectory (input: obj) : string option =
        if isNull input || isNull input?directory then
            None
        else
            let d = unbox<string> input?directory
            if String.IsNullOrWhiteSpace d then None else Some d

    /// PERSIST-004/005: a rejected boot is a startup failure, not an absent journal.
    ///
    /// `None` means "no workspace, so no runtime directory to own" — a legitimate
    /// state. A `FoldRejection` means the on-disk history could not be folded
    /// (mid-file corruption, pre-0.5.0 schema), and PERSIST-005 forbids guessing:
    /// swallowing it into `None` would start the plugin with an empty projection and
    /// then append new facts on top of a history it never read.
    let createJournal (input: obj) : Result<AgentJournal option, string> =
        match workspaceDirectory input with
        | None -> Ok None
        | Some workspace ->
            let commonDir = RuntimePath.gitCommonDir workspace
            let runtimeDir = RuntimePath.forWorkspace workspace
            let port = WorkspaceEventStore.bootPort commonDir

            let openJournal (runtimeId: RuntimeId) (processId: int) (startedAt: DateTimeOffset) =
                port.ResumeOrCreate(runtimeId, processId, startedAt)
                |> Result.bind (fun (writer, _, projection) -> AgentJournal.createFromProjection writer projection)

            match SharedAgentJournal.acquire runtimeDir processId DateTimeOffset.UtcNow openJournal with
            | Ok journal -> Ok(Some journal)
            | Error rejection ->
                Error(sprintf "journal boot rejected at %s: %s (%s)" runtimeDir rejection.Reason rejection.Fact)


    let restoreSessionParents (journal: AgentJournal option) (sessionParents: Dictionary<string, string>) =
        match journal with
        | None -> ()
        | Some journal ->
            let snapshot = AgentJournal.snapshot journal

            for KeyValue(parentId, session) in snapshot.AgentProjections.Sessions do
                match session.Handles with
                | Some handles ->
                    // Retired handles included: the parent relationship outlives the
                    // run (EXEC-009 tombstone), and dropping it here would orphan a
                    // finished child from its Manager.
                    for record in HandleProjection.linkedChildren handles do
                        let childKey = SessionId.value record.ChildSessionId
                        sessionParents.[childKey] <- SessionId.value parentId
                        SessionExecutionBinding.restore parentId record.ChildSessionId (Some record.TargetAgent)
                | None -> ()

            for KeyValue(sessionId, association) in snapshot.AgentProjections.Associations do
                match association.ParentSessionId with
                | Some parentId ->
                    let sessionKey = SessionId.value sessionId
                    sessionParents.[sessionKey] <- SessionId.value parentId

                    let agent =
                        PromptAuthorityLedger.activeProfile sessionId snapshot.AgentProjections
                        |> Option.orElseWith (fun () ->
                            PromptAuthorityLedger.lastAuthorityProfile sessionId snapshot.AgentProjections)
                        |> Option.map (fun profile -> profile.SelectedAgent)

                    SessionExecutionBinding.restore parentId sessionId agent
                | None -> ()

    let gitTreePortFromInput (input: obj) : GitTreePort option =
        if isNull input || isNull input?gitTreePort || isNull input?gitTreePort?getTreeHash then
            None
        else
            Some { GetTreeHash = (fun () -> unbox<string> (input?gitTreePort?getTreeHash ())) }

    let createHost
        (input: obj)
        (portOpt: IOpenCodePort option)
        (familyParent: (SessionId -> SessionId option) option)
        : Result<
              IEventObservationPort *
              ISessionHostPort *
              ISessionSnapshotPort option *
              string option *
              Events.HostEventPort option,
              string
           >
        =
        // Completion/output port is process-local per workspace runtime path so
        // root + worktree plugin instances share AwaitAgent / NotifyTerminal.
        // Host SSE remains only a coarse idle/retry/deleted signal source.
        let shared = SharedTerminalBus.tryAcquireForWorkspace (workspaceDirectory input)

        let hostEventPort, terminalKey =
            match shared with
            | Some(key, port) -> port, Some key
            | None -> Events.HostEventPort(), None

        let eventPort = hostEventPort :> IEventObservationPort

        let sessionPort =
            InjectedSessionPort(portOpt, eventPort, ?familyParent = familyParent) :> ISessionHostPort

        let snapshotPort = SessionSnapshotPort.create input
        Ok(eventPort, sessionPort, snapshotPort, terminalKey, Some hostEventPort)
