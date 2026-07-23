namespace Wanxiangshu.Next.Tools

open Wanxiangshu.Next.Kernel

type HostMessage =
    { Role: string
      Text: string
      ToolCalls: string list option
      Metadata: Map<string, string> option }

type SessionSnapshot =
    { Caps: string list
      ReviewContext: string option
      ParallelHint: string option }

type MessageWatermark = Index of int

module MessageTransform =

    let sanitize (messages: HostMessage list) : HostMessage list =
        messages
        |> List.filter (fun m ->
            let hasText = not (System.String.IsNullOrWhiteSpace(m.Text))

            let hasTools =
                match m.ToolCalls with
                | Some t -> not (List.isEmpty t)
                | None -> false

            hasText || hasTools)

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

                Some
                    { Role = "system"
                      Text = capsText
                      ToolCalls = None
                      Metadata = None }

        let reviewSystemMsg =
            match snapshot.ReviewContext with
            | None -> None
            | Some ctx ->
                Some
                    { Role = "system"
                      Text = sprintf "[REVIEW: %s]" ctx
                      ToolCalls = None
                      Metadata = None }

        let hintSystemMsg =
            match snapshot.ParallelHint with
            | None -> None
            | Some hint ->
                Some
                    { Role = "system"
                      Text = sprintf "[HINT: %s]" hint
                      ToolCalls = None
                      Metadata = None }

        let systemMsgs = [ capsSystemMsg; reviewSystemMsg; hintSystemMsg ] |> List.choose id

        systemMsgs @ baseMsgs

    let replacePrefix (messages: HostMessage list) (bRecord: string) (watermark: MessageWatermark) : HostMessage list =
        match watermark with
        | Index idx ->
            if idx < 0 || idx >= List.length messages then
                messages
            else
                let tail = messages |> List.skip (idx + 1)

                let syntheticMsg =
                    { Role = "system"
                      Text = bRecord
                      ToolCalls = None
                      Metadata = None }

                syntheticMsg :: tail
