namespace Wanxiangshu.Next.Tools

open System
open Wanxiangshu.Next.Kernel

type HostMessage =
    { Role: string
      Text: string
      ToolCalls: string list option
      Metadata: Map<string, string> option }

type MessageWatermark = Index of int

module MessageTransform =

    let roleOfAgent (agent: string option) : Role option =
        match agent with
        | None -> None
        | Some value when String.IsNullOrWhiteSpace value -> None
        | Some value ->
            match value.Trim().ToLowerInvariant() with
            | "manager" -> Some Role.Manager
            | "coder" -> Some Role.Coder
            | "orchestrator" -> Some Role.Orchestrator
            | "inspector" -> Some Role.Inspector
            | "browser" -> Some Role.Browser
            | "meditator" -> Some Role.Meditator
            | "reviewer" -> Some Role.Reviewer
            | "executor" -> Some Role.Executor
            | "blogger" -> Some Role.Blogger
            | _ -> None

    let companionAllowedRole (role: Role) : bool =
        match role with
        | Role.Manager
        | Role.Coder
        | Role.Orchestrator -> true
        | Role.Blogger
        | Role.Executor
        | Role.Inspector
        | Role.Browser
        | Role.Meditator
        | Role.Reviewer -> false

    let shouldCreateCompanion (agent: string option) : bool =
        agent |> roleOfAgent |> Option.exists companionAllowedRole

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
