namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation.Identity

/// JS-native state boundary for the session persona binding law.
/// Session ids and persona values are strings; the process-local registry and
/// its bind-once invariant remain private to SessionPersona.
module SessionPersonaSurface =

    let clear () : unit = SessionPersona.clearAllForTests()

    let tryGet (sessionId: string) : string =
        SessionPersona.tryGet(SessionId.create sessionId) |> Option.defaultValue ""

    let bindOnce (sessionId: string) (persona: string) : obj =
        match SessionPersona.bindOnce (SessionId.create sessionId) persona with
        | Ok value -> box {| ok = true; value = value; error = "" |}
        | Error message -> box {| ok = false; value = ""; error = message |}

    let inheritFromOwner (ownerPersona: string) (childSessionId: string) : obj =
        match SessionPersona.inheritFromOwner ownerPersona (SessionId.create childSessionId) with
        | Ok value -> box {| ok = true; value = value; error = "" |}
        | Error message -> box {| ok = false; value = ""; error = message |}
