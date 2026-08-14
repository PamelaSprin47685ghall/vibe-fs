namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

module HostSessionContext =

    /// Resolve Role from Host Agent identity (fast-ROLE / deep-ROLE) or Canonical Role.
    /// build/plan aliases remain rejected.
    let roleOf (agent: string) =
        if isNull agent || String.IsNullOrWhiteSpace agent then
            None
        else
            AgentRoleIdentity.roleOfString agent

    let read raw =
        let event = if isNull raw || isNull raw?event then raw else raw?event
        let properties = if isNull event then null else event?properties

        let sessionId =
            if not (isNull properties) && not (isNull properties?sessionID) then
                unbox<string> properties?sessionID
            elif not (isNull event) && not (isNull event?sessionID) then
                unbox<string> event?sessionID
            else
                ""

        let role =
            if
                not (isNull properties)
                && not (isNull properties?info)
                && not (isNull properties?info?agent)
            then
                Some(unbox<string> properties?info?agent)
            else
                None

        sessionId, role
