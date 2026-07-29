namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module PluginHost =

    [<Import("pid", "node:process")>]
    let processId: int = jsNative

    let workspaceDirectory (input: obj) : string option =
        if isNull input || isNull input?directory then
            None
        else
            let d = unbox<string> input?directory
            if String.IsNullOrWhiteSpace d then None else Some d

    let createJournal (input: obj) : AgentJournal option =
        match workspaceDirectory input with
        | None -> None
        | Some workspace ->
            let dir = RuntimePath.forWorkspace workspace
            Some(SharedAgentJournal.acquire dir processId DateTimeOffset.UtcNow)


    let restoreSessionRoles (journal: AgentJournal option) (sessionRoles: Dictionary<string, string>) =
        match journal with
        | None -> ()
        | Some journal ->
            let snapshot = AgentJournal.snapshot journal

            for KeyValue(sid, session) in snapshot.AgentProjections.Sessions do
                match session.Linkage with
                | Some linkage ->
                    sessionRoles.[SessionId.value sid] <- "manager"

                    for KeyValue(childId, role) in linkage.LinkedRoles do
                        sessionRoles.[ChildId.value childId] <- role.Trim().ToLowerInvariant()
                | None -> ()

    let restoreSessionParents (journal: AgentJournal option) (sessionParents: Dictionary<string, string>) =
        match journal with
        | None -> ()
        | Some journal ->
            let snapshot = AgentJournal.snapshot journal

            for KeyValue(parentId, session) in snapshot.AgentProjections.Sessions do
                match session.Linkage with
                | Some linkage ->
                    for KeyValue(childId, _) in linkage.LinkedChildren do
                        sessionParents.[ChildId.value childId] <- SessionId.value parentId
                | None -> ()

    let gitTreePortFromInput (input: obj) : GitTreePort option =
        if isNull input || isNull input?gitTreePort || isNull input?gitTreePort?getTreeHash then
            None
        else
            Some { GetTreeHash = (fun () -> unbox<string> (input?gitTreePort?getTreeHash ())) }

    let createSpikeHost (portOpt: IOpenCodePort option) =
        let eventPort = Events.DeterministicEventPort() :> IEventObservationPort
        let sessionPort = InjectedSessionPort(portOpt, eventPort) :> ISessionHostPort
        eventPort, sessionPort

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
