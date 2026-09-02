namespace Wanxiangshu.OpenCode

/// mv / rm — Coder-only file mutation tools (AGENT-016/017/018).
///
/// Both map to the POSIX command of the same name, implemented over Node's
/// cross-platform fs API (renameSync / rmSync), so no shell is involved and
/// path semantics do not depend on the platform's command line.
module FileMutationTools =
    val mvAdmission: ToolAdmission
    val rmAdmission: ToolAdmission
    val mvSpec: factory: HostToolFactory -> ToolSpec
    val rmSpec: factory: HostToolFactory -> ToolSpec
