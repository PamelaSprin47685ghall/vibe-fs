namespace Wanxiangshu.OpenCode.Host

module TodoWriteCompressionContract =
    val decorateDefinition: toolInput: obj -> toolOutput: obj -> unit
    val captureAndHide: args: obj -> Result<int, string>
    val restore: args: obj -> unit
