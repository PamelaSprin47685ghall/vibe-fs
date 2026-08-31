namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation.Identity

/// JS-native state boundary for the session persona binding law.
/// Session ids and persona values cross as strings and results as plain objects.
module SessionPersonaSurface =

    let private success (persona: Persona) : obj =
        box
            {| ok = true
               value = Persona.render persona
               error = "" |}

    let private failure (message: string) : obj =
        box
            {| ok = false
               value = ""
               error = message |}

    let clear () : unit = SessionPersona.clearAllForTests ()

    let drop (sessionId: string) : unit =
        SessionPersona.drop (SessionId.create sessionId)

    let tryGet (sessionId: string) : string =
        SessionPersona.tryGet (SessionId.create sessionId)
        |> Option.map Persona.render
        |> Option.defaultValue ""

    let private bindPersona (sessionId: string) (persona: Persona) : obj =
        match SessionPersona.bindOnce (SessionId.create sessionId) persona with
        | Ok value -> success value
        | Error rejection -> failure (PersonaRejection.render rejection)

    let bindOnce (sessionId: string) (personaLabel: string) : obj =
        match Persona.tryParse personaLabel with
        | None -> failure (sprintf "Invalid persona '%s'." personaLabel)
        | Some persona -> bindPersona sessionId persona

    let inheritFromOwner (ownerSessionId: string) (childSessionId: string) : obj =
        match SessionPersona.inheritFromOwner (SessionId.create ownerSessionId) (SessionId.create childSessionId) with
        | Ok value -> success value
        | Error rejection -> failure (PersonaRejection.render rejection)
