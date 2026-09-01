namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

module HostSessionContext =

    let private parseRole (agent: string) : Role option =
        match ManagedAgent.tryParse agent with
        | Some managed -> Some managed.Role
        | None -> Roles.tryParseRole (agent.Trim())

    /// Resolve Role from Host Agent identity (fast-ROLE / deep-ROLE) or Canonical Role.
    /// build/plan aliases remain rejected.
    let roleOf (agent: string) : Role option =
        Option.ofObj agent
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.bind parseRole

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
