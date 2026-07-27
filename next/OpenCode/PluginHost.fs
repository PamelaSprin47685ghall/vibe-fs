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

    let private journalGate = obj ()
    let private journals = Dictionary<string, AgentJournal>()

    let createJournal (input: obj) : AgentJournal option =
        match workspaceDirectory input with
        | None -> None
        | Some workspace ->
            let dir = RuntimePath.forWorkspace workspace

            lock journalGate (fun () ->
                match journals.TryGetValue dir with
                | true, journal -> Some journal
                | false, _ ->
                    // ponytail: one process/runtime writer per Git common dir;
                    // add reference counting only if hosts unload repos in-process.
                    let boot = Boot.boot dir
                    let runtimeId = RuntimeId.create (Guid.NewGuid().ToString("N").Substring(0, 12))

                    let journal =
                        AgentJournal.createFromBoot dir runtimeId processId DateTimeOffset.UtcNow boot

                    journals.[dir] <- journal
                    Some journal)

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
        (onDirectory: (obj -> unit) option)
        : Result<IEventObservationPort * ISessionHostPort * IDisposable option * (obj -> unit) option, string> =
        // Always keep the plugin event-hook observer: it covers main-directory
        // sessions. Also open /global/event when available so worktree-scoped
        // manager/coder/reviewer terminals still complete HostForkRuntime runs.
        let hostEventPort = Events.HostEventPort()
        let eventPort = hostEventPort :> IEventObservationPort
        let sessionPort = InjectedSessionPort(portOpt, eventPort) :> ISessionHostPort
        let observe = Some(fun raw -> HostEventSubscribe.observe hostEventPort raw)

        match HostEventSubscribe.trySubscribeHostEvents input hostEventPort onDirectory with
        | Error err -> Error err
        | Ok subscription -> Ok(eventPort, sessionPort, subscription, observe)
