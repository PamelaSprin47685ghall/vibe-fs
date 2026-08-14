namespace Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

open Wanxiangshu.Foundation

/// DSL-class: Vocabulary — filesystem primitive capabilities projected into
/// the js-ROLE SDK. Pure projection of ToolPermission; never decided here
/// (JS-001: no second permission matrix).
[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write
    | Edit
    | Glob
    | Grep

module JsCapability =

    /// The only ToolPermission → JsCapability mapping. Non-filesystem
    /// permissions produce no js-* member (JS-004 four-layer exactness).
    let ofToolPermission (permission: ToolPermission) : JsCapability option =
        match permission with
        | ToolPermission.Read -> Some JsCapability.Read
        | ToolPermission.Write -> Some JsCapability.Write
        | ToolPermission.Edit -> Some JsCapability.Edit
        | ToolPermission.Glob -> Some JsCapability.Glob
        | ToolPermission.Grep -> Some JsCapability.Grep
        | _ -> None

    let ofToolCapabilities (capabilities: Set<ToolPermission>) : Set<JsCapability> =
        capabilities |> Set.toList |> List.choose ofToolPermission |> Set.ofList

    /// Fixed canonical member order (JS-002): file, glob, grep, rewrite, write.
    let order (capability: JsCapability) : int =
        match capability with
        | JsCapability.Read -> 0
        | JsCapability.Glob -> 1
        | JsCapability.Grep -> 2
        | JsCapability.Edit -> 3
        | JsCapability.Write -> 4

/// One capability's complete SDK projection: the single source for member
/// name, description, canonical example, and runtime binding key. The four
/// layers must stay identical or the surface lies (JS-002/004).
type JsCapabilityFragment =
    {
        Capability: JsCapability
        MemberName: string
        /// LLM-visible method signature in the public base class (JS-002).
        Signature: string
        Description: string
        CanonicalExample: string
        RuntimeBindingKey: string
    }

/// Fixed registry of all fragments: the only place a js-* member can be born.
module JsFragmentRegistry =

    let read: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "file"
          Signature = "async file(path, matches = [])"
          Description =
            "file(path, matches = []) — read a strict-UTF-8 immutable snapshot and "
            + "optionally resolve ordered begin/end anchors. Returns FileView with text(from, to)."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const file = await this.file(\"README.md\");\n"
            + "    return file.text();\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.read" }

    let glob: JsCapabilityFragment =
        { Capability = JsCapability.Glob
          MemberName = "glob"
          Signature = "async glob(pattern)"
          Description =
            "glob(pattern) — gitignore-style bounded path enumeration. Returns "
            + "{ paths, truncated }. Does not grant Read on those paths."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.glob(\"src/**/*.fs\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.glob" }

    let grep: JsCapabilityFragment =
        { Capability = JsCapability.Grep
          MemberName = "grep"
          Signature = "async grep(needle, pattern = \"**/*\")"
          Description =
            "grep(needle, pattern = \"**/*\") — search UTF-8 files selected by the same "
            + "gitignore-style glob. needle is a non-empty string or RegExp. Returns "
            + "{ matches, truncated } with 1-based line/column. Does not grant file()."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.grep(/TODO:.+/, \"src/**/*.js\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.grep" }

    let rewrite: JsCapabilityFragment =
        { Capability = JsCapability.Edit
          MemberName = "rewrite"
          Signature = "rewrite(path, newText)"
          Description =
            "rewrite(path, newText) — stage replacement of an existing UTF-8 file. "
            + "Missing target is FILE_NOT_FOUND. Commits with the program (all-or-nothing)."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const file = await this.file(\"src/foo.js\", [\n"
            + "      [\"begin\", \"end\", \"oldString\"],\n"
            + "    ]);\n"
            + "    this.rewrite(\n"
            + "      \"src/foo.js\",\n"
            + "      file.text(\"^\", \"begin\")\n"
            + "        + \"newString\"\n"
            + "        + file.text(\"end\", \"$\")\n"
            + "    );\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.edit" }

    let write: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "write"
          Signature = "write(path, newText)"
          Description =
            "write(path, newText) — stage creation of a missing UTF-8 file. "
            + "Existing target is FILE_ALREADY_EXISTS. Commits with the program."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.write(\n"
            + "      \"generated/version.txt\",\n"
            + "      \"1.2.3\\n\"\n"
            + "    );\n"
            + "    return { created: \"generated/version.txt\" };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.write" }

    let all: JsCapabilityFragment list = [ read; glob; grep; rewrite; write ]

    let byCapability: Map<JsCapability, JsCapabilityFragment> =
        all |> List.map (fun fragment -> fragment.Capability, fragment) |> Map.ofList
