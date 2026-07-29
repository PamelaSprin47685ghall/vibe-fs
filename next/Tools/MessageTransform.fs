namespace Wanxiangshu.Next.Tools

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.OpenCode

type HostMessage =
    { Role: string
      Text: string
      ToolCalls: string list option
      Metadata: Map<string, string> option }

type MessageWatermark = Index of int

module MessageTransform =

    /// Companion eligibility uses Canonical Role. Accept exact Managed Agent
    /// names (fast-* / deep-*). Bare canonical roles and build/plan aliases are
    /// rejected.
    let roleOfAgent (agent: string option) : Role option =
        match agent with
        | None -> None
        | Some value when String.IsNullOrWhiteSpace value -> None
        | Some value -> ManagedAgent.tryParse value |> Option.map (fun managed -> managed.Role)

    let companionAllowedRole (role: Role) : bool =
        RoleDefinitions.forRole role |> Option.exists (fun definition -> definition.Companion)

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
