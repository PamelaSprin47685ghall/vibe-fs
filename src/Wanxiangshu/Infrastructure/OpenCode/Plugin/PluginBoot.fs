namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Git
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process

module PluginBoot =

    /// Boot-time facts captured once from the raw plugin input, before any host
    /// wiring. Global initialization order stays authoritative here (Wave 3):
    /// resource install → port → journal → scope → parent restore → workspace.
    type Boot =
        { Input: obj
          PortOpt: IOpenCodePort option
          Journal: AgentJournal option
          Scope: PluginRuntimeScope
          Clock: IClockPort
          StrengthFailClosed: string -> unit
          WorkspaceDirectory: string option
          GitTreePort: Wanxiangshu.Review.GitTreePort option
          FamilyParent: SessionId -> SessionId option }

    let create (input: obj) : Task<Boot> =
        task {
            // Fail-fast resource load before any consumer (StaticTools / BlogTool / EnforcerHost).
            RuntimeResources.install (RuntimeResources.load ())

            let portOpt = OpenCodePort.create input

            let! journalResult = PluginHost.createJournal input

            let journal =
                match journalResult with
                | Ok value -> value
                | Error err -> raise (InvalidOperationException err)

            let scope = new PluginRuntimeScope(journal)
            let clock = PtyTiming.nodeClockPort ()

            let strengthFailClosed (reason: string) : unit =
                scope.Strength.TripStrengthFuse reason
                raise (InvalidOperationException reason)

            PluginHost.restoreSessionParents journal scope.Sessions.SessionParents

            let familyParent (sessionId: SessionId) =
                match scope.Sessions.SessionParents.TryGetValue(SessionId.value sessionId) with
                | true, parentId -> Some(SessionId.create parentId)
                | false, _ -> None

            // The stable workspace, captured once at plugin init. The transform
            // input carries no directory; the blogger must be pinned to this
            // path (not the manager worktree) so its system prompt survives the
            // worktree release at publish. First boot wins: the main workspace
            // instance starts before the manager worktree instances.
            let workspaceDirectory = PluginHost.workspaceDirectory input

            // DURABLE-CONVERGENCE-008: startup only ensures the external Git-hook
            // membrane. Actual sync runs later in the user's Git hook process and
            // must not depend on this OpenCode/Wanxiangshu process remaining alive.
            match workspaceDirectory with
            | None -> ()
            | Some workspace ->
                match HookDispatcher.ensure workspace with
                | Ok() -> ()
                | Error error -> raise (InvalidOperationException error)

            let gitTreePort =
                match PluginHost.gitTreePortFromInput input with
                | Some port -> Some port
                | None -> workspaceDirectory |> Option.map GitTree.create

            return
                { Input = input
                  PortOpt = portOpt
                  Journal = journal
                  Scope = scope
                  Clock = clock
                  StrengthFailClosed = strengthFailClosed
                  WorkspaceDirectory = workspaceDirectory
                  GitTreePort = gitTreePort
                  FamilyParent = familyParent }
        }
