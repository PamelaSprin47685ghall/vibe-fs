namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Git
open Wanxiangshu.Resources
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process

module PluginBoot =

    /// Load-time capabilities captured from raw plugin input. This phase may read
    /// durable state but never repairs old work, mutates the workspace, or calls Host sessions.
    type Boot =
        { Input: obj
          PortOpt: IOpenCodePort option
          Journal: AgentJournal option
          Scope: PluginRuntimeScope
          Clock: IClockPort
          StrengthFailClosed: string -> unit
          WorkspaceDirectory: string option
          FamilyParent: SessionId -> SessionId option }

    let create (input: obj) : Task<Boot> =
        task {
            // Fail-fast resource load before any consumer (StaticTools / BlogTool / EnforcerHost).
            RuntimeResources.install (RuntimeResources.load ())

            // EMR-001: bootstrap/load the sole model scheduler during Load Phase.
            // This may create the missing user config atomically, but performs no Host call.
            do! ModelRouting.initialize ()

            let portOpt = OpenCodePortAdapter.create input

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

            return
                { Input = input
                  PortOpt = portOpt
                  Journal = journal
                  Scope = scope
                  Clock = clock
                  StrengthFailClosed = strengthFailClosed
                  WorkspaceDirectory = workspaceDirectory
                  FamilyParent = familyParent }
        }
