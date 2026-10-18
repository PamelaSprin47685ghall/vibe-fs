namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop

/// Host-declared external tool namespaces (capability-enforcement-007).
///
/// The live Host config's `mcp` map is the only authority for which external
/// integration namespaces exist. This module never invents one, keeps no
/// per-tool and no per-source permission table, and never reads a second
/// source for the same question.
///
/// The Host names every MCP tool `<server>_<tool>`, so one pattern per declared
/// server covers that whole namespace — including a tool discovered after
/// `mcp.tools.changed`, which stays in the same namespace.
///
/// Two lifetimes meet here, and they are deliberately not the same computed
/// value:
///
///  * the projection (`ManagedAgentConfig`) writes the patterns of ONE config
///    application onto that application's agent entries, and
///  * the admission gate (`ExternalToolAdmission`) must still match a session
///    whose permission map was written by an EARLIER application.
///
/// A session's map outlives the config hook that produced it: a reload can
/// withdraw a server (`resolve` drops `enabled = false`), and HOST-012 shares
/// these module singletons across every per-directory plugin instance, so one
/// process projects several workspaces whose `mcp` maps differ. The declared set
/// is therefore monotone — later applications may add a namespace, never
/// withdraw one — so the gate's truth always covers every map this process has
/// written. That is the fail-closed direction capability-enforcement-002 asks
/// for ("异常绕过 Schema 的调用亦在运行时 Gate 被即时阻断"): a namespace no live
/// map advertises is still refused rather than silently admitted.
[<RequireQualifiedAccess>]
module ExternalToolNamespace =

    // DSL-MUTABLE: resource — the process-wide declared external namespaces.
    let mutable private declaredNamespaces: string list = []

    /// A server the Host config declares as absent is not connected, so it
    /// declares no namespace; anything else the config lists is taken as
    /// declared, without inspecting its type.
    let private enabledServerNames (mcp: obj) : string[] =
        emitJsExpr
            mcp
            "Object.keys($0).filter(function (name) { var entry = $0[name]; return entry == null || entry.enabled !== false }).sort()"

    let resolve (config: obj) : string list =
        let mcp = if isNull config then null else config?mcp

        if isNull mcp then
            []
        else
            enabledServerNames mcp |> Array.toList

    let private patternOf (serverName: string) : string = serverName + "_*"

    let patternsOf (namespaces: string list) : string list = namespaces |> List.map patternOf

    let declare (namespaces: string list) : unit =
        declaredNamespaces <- List.distinct (declaredNamespaces @ namespaces)

    let matches (toolName: string) : bool =
        declaredNamespaces
        |> List.exists (fun server -> toolName.StartsWith(server + "_", StringComparison.Ordinal))
