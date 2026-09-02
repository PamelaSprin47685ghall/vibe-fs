namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System.Threading.Tasks

/// Opaque JS-native harness for the real Manager fork tool path.
/// Production semantics stay in ForkTool/HostForkRuntime; this surface only
/// supplies a physical Host boundary for executable requirement proofs.
module ForkToolSurface =

    val createRuntime: directory: string -> owners: obj -> Task<obj>

    val executeManagerFork:
        value: obj ->
        toolModule: obj ->
        owner: string ->
        calling: string ->
        byname: string ->
        charge: string ->
            Task<string>

    val captureOwnerOpening: value: obj -> owner: string -> text: string -> Task
    val captureOwnerDeltaPart: value: obj -> owner: string -> text: string -> providerRun: string -> Task
    val childCount: value: obj -> int
    val abortCount: value: obj -> int
    val child: value: obj -> obj
    val promptCount: value: obj -> int
    val awaitPromptCount: value: obj -> count: int -> Task
    val acceptPrompt: value: obj -> index: int -> bool
    val prompt: value: obj -> index: int -> obj
    val nextPromptAcceptanceUnknown: value: obj -> reason: string -> unit
    val nextPromptAdmittedWithReceipt: value: obj -> receipt: string -> unit
    val cancelOwnerChildren: value: obj -> owner: string -> Task
    val detachToolRuntime: value: obj -> Task
    val durableLifecycleByname: value: obj -> owner: string -> byname: string -> obj
    val executeHorizon: value: obj -> owner: string -> Task<string>
    val settle: value: obj -> owner: string -> answer: string -> providerRun: string -> Task<bool>
    val disposeRuntime: value: obj -> unit
