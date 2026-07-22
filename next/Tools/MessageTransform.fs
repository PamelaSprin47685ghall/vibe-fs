namespace Wanxiangshu.Next.Tools

open Wanxiangshu.Next.Kernel

type HostMessage = { Role: string; Text: string }

type SessionSnapshot =
    { Caps: string list
      ReviewContext: string option
      ParallelHint: string option }

module MessageTransform =

    let sanitize (messages: HostMessage list) : HostMessage list =
        messages
        |> List.filter (fun m -> not (System.String.IsNullOrWhiteSpace(m.Text)))

    let stripSystemMarkers (messages: HostMessage list) : HostMessage list =
        messages
        |> List.filter (fun m ->
            if m.Role <> "system" then
                true
            else
                let t = m.Text
                not (t.StartsWith("[CAPS:") || t.StartsWith("[REVIEW:") || t.StartsWith("[HINT:")))

    let transform (snapshot: SessionSnapshot) (messages: HostMessage list) : HostMessage list =
        let baseMsgs = messages |> sanitize |> stripSystemMarkers

        let capsSystemMsg =
            if List.isEmpty snapshot.Caps then
                None
            else
                let capsText = sprintf "[CAPS: %s]" (String.concat ", " snapshot.Caps)
                Some { Role = "system"; Text = capsText }

        let reviewSystemMsg =
            match snapshot.ReviewContext with
            | None -> None
            | Some ctx ->
                Some
                    { Role = "system"
                      Text = sprintf "[REVIEW: %s]" ctx }

        let hintSystemMsg =
            match snapshot.ParallelHint with
            | None -> None
            | Some hint ->
                Some
                    { Role = "system"
                      Text = sprintf "[HINT: %s]" hint }

        let systemMsgs = [ capsSystemMsg; reviewSystemMsg; hintSystemMsg ] |> List.choose id

        systemMsgs @ baseMsgs
