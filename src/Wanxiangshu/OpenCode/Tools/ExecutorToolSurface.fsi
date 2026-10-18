namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// JS-native semantic surface for bounded execution and raw output tailing.
/// A JS test never
/// constructs `runSpec`, a ToolHostCodec factory, ToolRuntimeScope or a
/// recovery union: those remain owner-private here.
module ExecutorToolSurface =

    /// Provider-visible execution verb.
    val runToolName: string

    /// Plain metadata for the provider-visible run contract.
    val describeRun: toolModule: obj -> obj

    /// Execute the provider-visible run contract. `toolModule` is the Host's
    /// schema module, `sessions` is an opaque Host session capability, `args`
    /// and `context` are plain Host objects, and `recovery` is the owner-owned
    /// recovery mode used by tests/canaries ("blocked", "waiting", or "ready").
    val run: toolModule: obj -> sessions: obj -> args: obj -> context: obj -> recovery: string -> Task<string>

    val formatSpooledOutcome: exitCode: int -> summary: string -> string
    val retainOutputTail: limitBytes: int -> output: string -> string
