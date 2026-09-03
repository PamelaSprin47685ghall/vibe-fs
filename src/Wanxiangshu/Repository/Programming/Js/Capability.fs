namespace Wanxiangshu.Repository.Programming.Js

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

    /// Fixed canonical capability-family order (JS-002). A capability may
    /// project more than one ordered member (Edit → edit, rewrite).
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
            "glob(pattern) — gitignore-style path enumeration. Returns "
            + "{ paths }. Does not grant Read on those paths."
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
            + "{ matches } with 1-based line/column. Does not grant file()."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.grep(/TODO:.+/, \"src/**/*.js\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.grep" }

    let edit: JsCapabilityFragment =
        { Capability = JsCapability.Edit
          MemberName = "edit"
          Signature = "edit(path, changes)"
          Description =
            "edit(path, changes) — atomically apply one { find, put, all? } change or a non-empty "
            + "array against one immutable target snapshot. Exact by default; 0, ambiguous, or "
            + "overlapping matches fail before staging."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.edit(\"src/foo.js\", [\n"
            + "      { find: \"const oldName = 1;\", put: \"const newName = 1;\" },\n"
            + "    ]);\n"
            + "    return { edited: \"src/foo.js\" };\n"
            + "  }\n"
            + "}"
          // edit() is a derived SDK affordance over the same guarded staging
          // executor as rewrite(); no second Edit permission exists.
          RuntimeBindingKey = "js.edit" }

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
            + "    this.rewrite(\"src/foo.js\", \"export const value = 2;\\n\");\n"
            + "    return { rewritten: \"src/foo.js\" };\n"
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

    /// Canonical member order inside capability families. Filtering this list
    /// is the deterministic projection; never rebuild member order elsewhere.
    let all: JsCapabilityFragment list = [ read; glob; grep; edit; rewrite; write ]

    let byCapability: Map<JsCapability, JsCapabilityFragment list> =
        all |> List.groupBy _.Capability |> Map.ofList
