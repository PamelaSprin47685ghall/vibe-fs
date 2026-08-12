namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// docs/what/enforcer.md — the `chronicle` tool (ENFORCER-010/020/040/041/061 tip v2).
///
/// Provider schema: required `entry` + required `tip` (enum = rule directory names).
/// Evidence field deleted (GrandRewrite). Execute never suspends (ENFORCER-040).
module BlogTool =

    /// ENFORCER-061: tool-visible rejection for empty canonical text.
    let EmptyTextError =
        "chronicle entry is empty after canonicalisation (ENFORCER-061)"

    /// No live Blogger cycle authority — reject, do not return OK.
    let NoLiveCycleError =
        "chronicle rejected: no live CurrentRequest (Blogger cycle not InFlight)"

    /// ENFORCER-022/061 pure gate — same trim/non-empty rule as EnforcerCodec.
    let tryCanonicalText (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error EmptyTextError
        else
            Ok trimmed

    /// Live cycle gate: physical flight ownership only (HasFlight).
    let hasLiveCycle (parkedHost: IParkedTransformHost option) (sessionId: string) : bool =
        match parkedHost with
        | None -> false
        | Some host -> host.HasFlight sessionId

    let private enforcerRules () =
        RuntimeResources.current().EnforcerRules

    /// ENFORCER-020/021: tip enum = rule directory TipNames in lexical order.
    let tipFieldNames () : string list =
        EnforcerCatalog.fieldNames (enforcerRules ())

    let private remembered () =
        ToolHostCodec.tomlObjectWithInstructions [ "# The Chronicle remembers this." ] []

    let private nothingToRemember () =
        ToolHostCodec.tomlObjectWithInstructions [ "# There is no occurrence here to remember." ] []

    let private unknownTip () =
        ToolHostCodec.tomlObjectWithInstructions [ "# That lesson is not in the Rulebook." ] []

    /// ENFORCER-020 JSON Schema shape via Host zod builder:
    /// required entry + tip enum.
    let spec
        (factory: HostToolFactory)
        (runtime: ToolRuntimeScope)
        (parkedHost: IParkedTransformHost option)
        : ToolSpec =
        let fields = tipFieldNames ()
        let ruleCount = List.length fields

        let catalogDescription =
            sprintf "Record one occurrence with required tip (exactly one of %d rulebook TipNames)." ruleCount

        { Name = "chronicle"
          Description = catalogDescription
          Arguments =
            [ "entry", ToolHostCodec.stringSchema factory
              "tip", ToolHostCodec.enumSchema fields factory ]
          Execute =
            fun args ctx ->
                task {
                    if not (hasLiveCycle parkedHost ctx.SessionId) then
                        Diagnostic.emit
                            "chronicle-execute"
                            [ "session_id", ctx.SessionId; "result", "no live CurrentRequest" ]

                        if not (String.IsNullOrWhiteSpace ctx.SessionId) then
                            let! _ = runtime.Sessions.AbortSession(SessionId.create ctx.SessionId)
                            ()

                        return raise (InvalidOperationException(NoLiveCycleError))
                    else
                        let entryRaw =
                            let typed = args.Text "entry"

                            if String.IsNullOrWhiteSpace typed then
                                args.Text "text"
                            else
                                typed

                        match tryCanonicalText entryRaw with
                        | Error _ -> return nothingToRemember ()
                        | Ok _ ->
                            let tipRaw = args.Text "tip"

                            if String.IsNullOrWhiteSpace tipRaw then
                                return unknownTip ()
                            else
                                match EnforcerCatalog.tryFindByField tipRaw (enforcerRules ()) with
                                | None -> return unknownTip ()
                                | Some _ -> return remembered ()
                } }
