namespace Wanxiangshu.Next.OpenCode

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Session

module HostSessionContext =

    let roleOf (agent: string) =
        match if isNull agent then "" else agent.Trim().ToLowerInvariant() with
        | "manager"
        | "build"
        | "plan" -> Some AgentRole.Manager
        | "orchestrator" -> Some AgentRole.Orchestrator
        | "coder" -> Some AgentRole.Coder
        | "inspector" -> Some AgentRole.Inspector
        | "browser" -> Some AgentRole.Browser
        | "meditator" -> Some AgentRole.Meditator
        | "reviewer" -> Some AgentRole.Reviewer
        | "devops" -> Some AgentRole.DevOps
        | "executor" -> Some AgentRole.Executor
        | "blogger" -> Some AgentRole.Blogger
        | _ -> None

    let canonicalRole (agent: string) =
        roleOf agent |> Option.map (fun role -> role.ToString().ToLowerInvariant())

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
