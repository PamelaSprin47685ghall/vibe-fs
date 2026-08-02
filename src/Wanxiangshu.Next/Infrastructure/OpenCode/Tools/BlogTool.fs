namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.EnforcerCatalogData
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Session

/// SSOT/15 — the `blog` tool (ENFORCER-010/020/040/041).
///
/// Provider schema: `text` (required), `evidence` (optional), plus the 120
/// canonical rule fields (optional 0..9 integers) generated from the one
/// catalog (ENFORCER-170). The execute body never suspends: it parses the raw
/// arguments, checks identity, and returns the fixed string "OK" — the Host's
/// tool-loop continuation depends on the promise resolving (ENFORCER-040).
///
/// The cycle merge happens later, at the continuation transform, where the
/// full provider step is re-read and re-canonicalised (ENFORCER-044) — nothing
/// of the merge may happen inside execute, or scheduling order would become a
/// business fact (ENFORCER-042).
module BlogTool =

    /// ENFORCER-170: the provider-visible argument schema is derived from the
    /// catalog — FieldName, ScoreWhen description, optional 0..9.
    let ruleArguments (factory: HostToolFactory) : (string * HostSchema) list =
        rules
        |> List.map (fun rule ->
            rule.FieldName,
            // Host schema has no bounded int; the 0..9 contract is enforced by
            // the codec (ENFORCER-023: out-of-range parses to zero, never
            // clamps). Description carries the rule's scoring contract.
            ToolHostCodec.optionalNumberSchema factory)

    /// ENFORCER-020/024: `text` and `evidence` are reserved keys and never
    /// take part in nearest-neighbour mapping.
    let spec (factory: HostToolFactory) (runtime: ToolRuntimeScope) : ToolSpec =
        let catalogDescription =
            sprintf
                "Record one work-log entry and score engineering practices 0..9 (%d rules; missing = 0)."
                (List.length rules)

        { Name = "blog"
          Description = catalogDescription
          Arguments =
            [ "text", ToolHostCodec.stringSchema factory
              "evidence", ToolHostCodec.optionalStringSchema factory ]
            @ ruleArguments factory
          Execute =
            fun args ctx ->
                task {
                    // ENFORCER-040: parse the raw arguments. The codec's
                    // tolerance work happens here too — a misspelled rule field
                    // must survive to the merge, so nothing is dropped here.
                    let text = args.Text "text"

                    match ctx.ProviderRunId, ctx.ToolCallId with
                    | Some _, Some _ ->
                        // ENFORCER-040: return the fixed string. The merge is
                        // the continuation transform's job (ENFORCER-044).
                        return ToolHostCodec.tomlObject [ "result", ToolHostCodec.TString "OK" ]
                    | _ ->
                        // ENFORCER-041: identity comes from ToolContext; the
                        // merge side re-derives it from the transcript (the
                        // part's callID + assistant message id), so a call with
                        // missing identity here is filtered out of the merge
                        // there, not committed. Execute still resolves with
                        // "OK" so the tool loop cannot stall.
                        Diagnostic.emit
                            "blog-execute"
                            [ "session_id", ctx.SessionId
                              "result", "blog call without ToolContext identity (ENFORCER-041)" ]

                        return ToolHostCodec.tomlObject [ "result", ToolHostCodec.TString "OK" ]
                } }
