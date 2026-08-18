namespace Wanxiangshu.OpenCode

open Fable.Core
open Wanxiangshu.Participant.Persona

/// JS-native Host session context boundary.
module HostSessionContextSurface =
    [<Emit("undefined")>]
    let private jsUndefined: obj = jsNative

    let roleOf (agent: string) : string option =
        HostSessionContext.roleOf agent |> Option.map AgentRoleIdentity.roleName

    let read (raw: obj) : obj =
        let sessionId, agent = HostSessionContext.read raw
        box [| box sessionId; agent |> Option.map box |> Option.defaultValue jsUndefined |]
