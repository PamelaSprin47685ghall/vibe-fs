namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Session

/// spec/15 — the `blog` tool (ENFORCER-010/020/040/041/061).
///
/// Provider schema: `text` (required), `evidence` (optional), plus the 120
/// canonical rule fields (optional 0..9 integers) from resources/enforcer/catalog.json
/// (ENFORCER-170). Execute never suspends (ENFORCER-040).
///
/// ENFORCER-061: empty/missing canonical text returns a readable error so the
/// Host tool-loop can repair once. Valid entry still returns fixed "OK".
/// Cycle merge stays at the continuation transform (ENFORCER-044).
///
/// Request-scoped capability: Role=Blogger is necessary but not sufficient.
/// Execute requires live CurrentRequest (InFlight) for the session; otherwise
/// returns a terminating protocol error (never "OK").
module BlogTool =

    /// ENFORCER-061: tool-visible rejection for empty canonical text.
    let EmptyTextError = "blog text is empty after canonicalisation (ENFORCER-061)"

    /// No live Blogger cycle authority — reject, do not return OK.
    let NoLiveCycleError =
        "blog rejected: no live CurrentRequest (Blogger cycle not InFlight)"

    /// ENFORCER-022/061 pure gate — same trim/non-empty rule as EnforcerCodec.decodeCall.
    let tryCanonicalText (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error EmptyTextError
        else
            Ok trimmed

    /// Live cycle gate: CurrentRequest present for session (InFlight payload).
    let hasLiveCycle (parkedHost: IParkedTransformHost option) (sessionId: string) : bool =
        match parkedHost with
        | None -> false
        | Some host -> host.TryPeekCurrentRequest sessionId |> Option.isSome

    /// ENFORCER-170: the provider-visible argument schema is derived from the
    /// catalog — FieldName, ScoreWhen description, optional 0..9.
    let private enforcerRules () =
        RuntimeResources.current().EnforcerRules

    let ruleArguments (factory: HostToolFactory) : (string * HostSchema) list =
        enforcerRules ()
        |> List.map (fun rule ->
            rule.FieldName,
            // Host schema has no bounded int; the 0..9 contract is enforced by
            // the codec (ENFORCER-023: out-of-range parses to zero, never
            // clamps). Description carries the rule's scoring contract.
            ToolHostCodec.optionalNumberSchema factory)

    /// ENFORCER-020/024: `text` and `evidence` are reserved keys and never
    /// take part in nearest-neighbour mapping.
    let spec
        (factory: HostToolFactory)
        (runtime: ToolRuntimeScope)
        (parkedHost: IParkedTransformHost option)
        : ToolSpec =
        let catalogDescription =
            sprintf
                "Record one work-log entry and score engineering practices 0..9 (%d rules; missing = 0)."
                (List.length (enforcerRules ()))

        { Name = "blog"
          Description = catalogDescription
          Arguments =
            [ "text", ToolHostCodec.stringSchema factory
              "evidence", ToolHostCodec.optionalStringSchema factory ]
            @ ruleArguments factory
          Execute =
            fun args ctx ->
                task {
                    if not (hasLiveCycle parkedHost ctx.SessionId) then
                        Diagnostic.emit
                            "blog-execute"
                            [ "session_id", ctx.SessionId; "result", "no live CurrentRequest" ]

                        return ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString NoLiveCycleError ]
                    else
                        // ENFORCER-061 first gate: reject empty canonical text before "OK".
                        match tryCanonicalText (args.Text "text") with
                        | Error err -> return ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString err ]
                        | Ok _ ->
                            match ctx.ProviderRunId, ctx.ToolCallId with
                            | Some _, Some _ ->
                                // ENFORCER-040: fixed OK. Merge is continuation's job (ENFORCER-044).
                                return ToolHostCodec.tomlObject [ "result", ToolHostCodec.TString "OK" ]
                            | _ ->
                                // ENFORCER-041: missing identity is filtered at merge, not here.
                                // Still resolve so the tool loop cannot stall.
                                Diagnostic.emit
                                    "blog-execute"
                                    [ "session_id", ctx.SessionId
                                      "result", "blog call without ToolContext identity (ENFORCER-041)" ]

                                return ToolHostCodec.tomlObject [ "result", ToolHostCodec.TString "OK" ]
                } }
