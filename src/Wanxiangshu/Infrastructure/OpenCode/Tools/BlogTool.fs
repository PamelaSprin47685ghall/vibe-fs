namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// spec/15 — the `blog` tool (ENFORCER-010/020/040/041/061 tip v2).
///
/// Provider schema: required `text` + required `tip` (enum = catalog fields),
/// optional `evidence`. No 120 numeric score fields (ENFORCER-020).
/// Execute never suspends (ENFORCER-040). Runtime re-validates tip
/// (ENFORCER-023) — schema alone is not trusted.
///
/// ENFORCER-061: empty/missing canonical text returns a readable error so the
/// Host tool-loop can repair once. Valid entry still returns fixed "OK".
/// Cycle merge stays at the continuation transform (ENFORCER-044).
module BlogTool =

    /// ENFORCER-061: tool-visible rejection for empty canonical text.
    let EmptyTextError = "blog text is empty after canonicalisation (ENFORCER-061)"

    /// No live Blogger cycle authority — reject, do not return OK.
    let NoLiveCycleError =
        "blog rejected: no live CurrentRequest (Blogger cycle not InFlight)"

    /// ENFORCER-022/061 pure gate — same trim/non-empty rule as EnforcerCodec.
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

    let private enforcerRules () =
        RuntimeResources.current().EnforcerRules

    /// ENFORCER-020/021: tip enum = catalog field names in ordinal order.
    let tipFieldNames () : string list =
        EnforcerCatalog.fieldNames (enforcerRules ())

    /// ENFORCER-020 JSON Schema shape via Host zod builder:
    /// required text + tip enum; optional evidence.
    let spec
        (factory: HostToolFactory)
        (runtime: ToolRuntimeScope)
        (parkedHost: IParkedTransformHost option)
        : ToolSpec =
        let fields = tipFieldNames ()
        let ruleCount = List.length fields

        let catalogDescription =
            sprintf
                "Record one work-log entry with required tip (exactly one of %d catalog fields). Optional evidence."
                ruleCount

        { Name = "blog"
          Description = catalogDescription
          Arguments =
            [ "text", ToolHostCodec.stringSchema factory
              "tip", ToolHostCodec.enumSchema fields factory
              "evidence", ToolHostCodec.optionalStringSchema factory ]
          // text + tip are required; evidence is optional (.optional() on schema).
          // Host schema surface uses isOptional(); bare string/enum = required.
          Execute =
            fun args ctx ->
                task {
                    if not (hasLiveCycle parkedHost ctx.SessionId) then
                        Diagnostic.emit
                            "blog-execute"
                            [ "session_id", ctx.SessionId; "result", "no live CurrentRequest" ]

                        if not (String.IsNullOrWhiteSpace ctx.SessionId) then
                            let! _ = runtime.Sessions.AbortSession(SessionId.create ctx.SessionId)
                            ()

                        return raise (InvalidOperationException(NoLiveCycleError))
                    else
                        match tryCanonicalText (args.Text "text") with
                        | Error err -> return ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString err ]
                        | Ok _ ->
                            // ENFORCER-023: runtime tip re-validation (do not trust schema alone).
                            let tipRaw = args.Text "tip"

                            if String.IsNullOrWhiteSpace tipRaw then
                                return
                                    ToolHostCodec.tomlObject
                                        [ "error", ToolHostCodec.TString EnforcerCodec.MissingTipError ]
                            else
                                match EnforcerCatalog.tryFindByField tipRaw (enforcerRules ()) with
                                | None ->
                                    return
                                        ToolHostCodec.tomlObject
                                            [ "error",
                                              ToolHostCodec.TString(EnforcerCodec.unknownTipError (tipRaw.Trim())) ]
                                | Some _ ->
                                    match ctx.ProviderRunId, ctx.ToolCallId with
                                    | Some _, Some _ ->
                                        // ENFORCER-040: fixed OK. Merge is continuation's job (ENFORCER-044).
                                        return ToolHostCodec.tomlObject [ "result", ToolHostCodec.TString "OK" ]
                                    | _ ->
                                        Diagnostic.emit
                                            "blog-execute"
                                            [ "session_id", ctx.SessionId
                                              "result", "blog call without ToolContext identity (ENFORCER-041)" ]

                                        return ToolHostCodec.tomlObject [ "result", ToolHostCodec.TString "OK" ]
                } }
