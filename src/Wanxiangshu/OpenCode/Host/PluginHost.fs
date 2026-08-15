namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Mission.Review
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

module PluginHost =

    [<Import("pid", "node:process")>]
    let processId: int = jsNative

    let workspaceDirectory (input: obj) : string option =
        if isNull input || isNull input?directory then
            None
        else
            let directory = unbox<string> input?directory

            if String.IsNullOrWhiteSpace directory then
                None
            else
                Some directory

    /// Load may read durable bytes, but it never repairs semantic state. Physical
    /// corruption is a load error; a domain fold/replay rejection only disables the
    /// journal capability for this plugin instance.
    let createJournal (input: obj) : Task<Result<AgentJournal option, string>> =
        task {
            match workspaceDirectory input with
            | None -> return Ok None
            | Some workspace ->
                let commonDir = RuntimePath.gitCommonDir workspace
                let runtimeDir = RuntimePath.forWorkspace workspace

                match ProcessEventLog.readStreams commonDir with
                | Error error -> return Error(sprintf "durable bytes unreadable at %s: %A" runtimeDir error)
                | Ok streams ->
                    let missingPayload =
                        streams
                        |> List.collect snd
                        |> List.collect (fun envelope -> envelope.PayloadRefs)
                        |> List.tryFind (ProcessEventLog.payloadExists commonDir >> not)

                    match missingPayload with
                    | Some payloadRef ->
                        return
                            Error(
                                sprintf
                                    "durable payload unreadable at %s: %s"
                                    runtimeDir
                                    (PayloadRef.value payloadRef)
                            )
                    | None ->
                        try
                            let port = WorkspaceEventStore.bootPort commonDir

                            let openJournal (runtimeId: RuntimeId) (processId: int) (startedAt: DateTimeOffset) =
                                task {
                                    match! port.ResumeOrCreate(runtimeId, processId, startedAt) with
                                    | Error err -> return Error err
                                    | Ok(writer, _, projection) -> return AgentJournal.createFromProjection writer projection
                                }

                            match! SharedAgentJournal.acquire runtimeDir processId DateTimeOffset.UtcNow openJournal with
                            | Ok journal -> return Ok(Some journal)
                            | Error rejection ->
                                Diagnostic.emit
                                    "journal-semantic-unavailable"
                                    [ "result", sprintf "%s (%s)" rejection.Reason rejection.Fact ]

                                return Ok None
                        with ex ->
                            Diagnostic.emit "journal-semantic-unavailable" [ "result", ex.Message ]
                            return Ok None
        }

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
