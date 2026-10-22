namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

open Wanxiangshu.Participant.ManagedAgents

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

module HostSessionContext =

    /// Pure roster law now owned by Participant.ManagedAgents.AgentRole (CUT-34).
    let roleOf (agent: string) : Role option = AgentRole.roleOf agent

    let read (raw: obj) =
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
