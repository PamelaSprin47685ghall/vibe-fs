namespace Wanxiangshu.OpenCode

/// JS-native semantic surface for the provider execution verb (PROC-011 /
/// DISTILL-010). The name is a string constant; distillation is invoked
/// inside `run` and is never a separate provider tool. A JS test never
/// constructs `runSpec` or a ToolHostCodec factory
/// (JS-SEMANTIC-SURFACE-003/005).
module ExecutorToolSurface =

    /// Provider-visible execution verb. Distillation is not a provider tool.
    let runToolName: string = ExecutorTool.RunToolName
