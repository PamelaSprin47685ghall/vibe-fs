namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Restart recovery for linked children: rebuild unjoined completions from
/// transcript when possible; otherwise mark Interrupted instead of faking Busy.
module HostForkRestart =

    let private textOfParts (parts: MessagePart array) =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (function
                | MessagePart.Text text -> Some text
                | _ -> None)
            |> String.concat ""

    let private lastByRole (messages: SessionMessage list) (role: string) =
        messages
        |> List.rev
        |> List.tryFind (fun message -> message.Role.Equals(role, StringComparison.OrdinalIgnoreCase))

    let private isTerminalCompleted (assistant: SessionMessage) =
        match assistant.Finish with
        | Some finish when finish.Equals("stop", StringComparison.OrdinalIgnoreCase) ->
            not (String.IsNullOrWhiteSpace(textOfParts assistant.Parts))
        | _ -> false

    let recoverChild
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (agentId: string)
        (childSessionId: SessionId)
        (role: AgentRole)
        (agent: string)
        : Task<unit> =
        task {
            runtime.Restore(agentId, role, agent)
            runtime.BindChildSession(agentId, SessionId.value childSessionId)

            match snapshot with
            | None -> runtime.MarkInterrupted(agentId, "host restart: no session snapshot")
            | Some port ->
                match! port.GetMessages childSessionId with
                | Error reason -> runtime.MarkInterrupted(agentId, sprintf "host restart: snapshot failed: %s" reason)
                | Ok messages ->
                    match lastByRole messages "assistant" with
                    | None -> ()
                    | Some assistant when isTerminalCompleted assistant ->
                        // The completion needs the Authority Root the child ran under,
                        // and that is the last physical user message in its transcript.
                        // A terminal assistant with no user message before it is not a
                        // state the Host produces; reporting it with a blank root would
                        // hand `join` a completion whose PROMPT-002 identity is empty.
                        match lastByRole messages "user" with
                        | None -> runtime.MarkInterrupted(agentId, "host restart: terminal child has no user message")
                        | Some user ->
                            let payload =
                                AgentCompletion.completed
                                    agentId
                                    (SessionId.value childSessionId)
                                    ("run-restored-" + agentId)
                                    role
                                    user.Id
                                    assistant.Id
                                    (textOfParts assistant.Parts)
                                    None
                                    None

                            runtime.PublishCompletion
                                { RunId = "run-restored-" + agentId
                                  AgentId = agentId
                                  AgentName = agent
                                  Role = role
                                  Outcome = payload
                                  CompletedAt = DateTimeOffset.UtcNow }
                    | Some assistant ->
                        runtime.MarkInterrupted(
                            agentId,
                            sprintf "host restart: child not terminal (finish=%s)" (defaultArg assistant.Finish "none")
                        )
        }

    let recoverAll
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (children: IDictionary<string, SessionId * AgentRole * string>)
        : Task<unit> =
        task {
            for KeyValue(agentId, (childSessionId, role, agent)) in children do
                do! recoverChild runtime snapshot agentId childSessionId role agent
        }

    /// EXEC-009 restart recovery: rebuild this parent's join mailbox from the
    /// durable handle records.
    ///
    /// Retired handles are skipped. EXEC-009 makes the tombstone permanent and
    /// forbids a retired id degrading back into a fork target, so restoring one
    /// would put a consumed completion back on the mailbox and let `join` return it
    /// a second time.
    ///
    /// PTY and ManagerJob handles are skipped too: this rebuilds agent children, and
    /// a PTY is re-owned by `PtyPort`, not by a transcript replay.
    let restoreLinkedChildren
        (runtime: ForkRuntime)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (parentId: SessionId)
        (children: Dictionary<string, SessionId>)
        (childCreatedDir: string -> SessionId -> string option -> unit)
        (directoryOf: string -> string option)
        : Task =
        task {
            let records =
                AgentProjection.tryFind parentId (AgentJournal.snapshot journal).AgentProjections
                |> Option.bind (fun session -> session.Handles)
                |> Option.map HandleProjection.linkedChildren
                |> Option.defaultValue []

            for record in records do
                match record.Lifecycle, HandleId.tryAgent record.Handle with
                | HandleLifecycle.Retired, _
                | _, None -> ()
                | _, Some agentHandle ->
                    let agentId = AgentHandleId.value agentHandle

                    // The role is the durable CanonicalRole. `TargetAgent` carries the
                    // managed agent name the fork selected, so neither is rebuilt from
                    // the other — PROMPT-008's pair stays exactly as recorded.
                    let role =
                        record.CanonicalRole
                        |> Option.bind PromptAuthority.tryParseRole
                        |> Option.map AgentRoleIdentity.ofRole

                    match role with
                    | None -> ()
                    | Some role ->
                        children.[agentId] <- record.ChildSessionId
                        childCreatedDir agentId record.ChildSessionId (directoryOf agentId)
                        do! recoverChild runtime snapshot agentId record.ChildSessionId role record.TargetAgent
        }
