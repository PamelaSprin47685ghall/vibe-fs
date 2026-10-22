namespace Wanxiangshu.OpenCode

module OpenCodePortAdapter =
    val create: workspaceDirectory: string option -> input: obj -> IOpenCodePort option
