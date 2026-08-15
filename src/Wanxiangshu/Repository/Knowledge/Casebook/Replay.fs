namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// CASE-004: replay stored observations against the current worktree —
/// read-only, never writing the subject. FileRead re-reads and re-hashes;
/// GlobResult re-enumerates; GrepResult re-searches. Any missing/changed
/// result makes the whole replay Stale (freshness hint, not proof).
module CasebookReplay =

    let private readHash (root: string) (path: string) : string option =
        match JsUtf8Fs.readUtf8Classified (JsMutationFs.resolveToolPath root path) with
        | Ok text -> Some(CasebookCapture.contentHash text)
        | Error _ -> None

    /// Replay one observation; None = the observation cannot be reproduced
    /// (missing file / unreadable) — that is a change signal.
    let replayOne (root: string) (observation: Observation) : Observation option =
        match observation with
        | Observation.FileRead(path, _) ->
            readHash root path |> Option.map (fun hash -> Observation.FileRead(path, hash))
        | Observation.GlobResult(pattern, _) ->
            match JsGlobFs.glob root pattern with
            | Ok listing -> Some(Observation.GlobResult(pattern, listing.Paths))
            | Error _ -> None
        | Observation.GrepResult(pattern, _) ->
            match JsAnchorFs.grep root (AnchorSpec.Regex pattern) "**/*" with
            | Ok listing ->
                let matches = listing.Matches |> List.map (fun hit -> hit.Path, hit.Line, hit.Text)

                Some(Observation.GrepResult(pattern, matches))
            | Error _ -> None

    /// Replay the whole stored observation set. Missing any single
    /// observation (deleted file, unreadable) → Stale.
    let replayAll (root: string) (stored: Observation list) : Observation list = stored |> List.choose (replayOne root)
