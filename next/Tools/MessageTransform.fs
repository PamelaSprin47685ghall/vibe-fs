namespace Wanxiangshu.Next.Tools

type HostMessage =
    { Role: string
      Text: string
      ToolCalls: string list option
      Metadata: Map<string, string> option }

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
