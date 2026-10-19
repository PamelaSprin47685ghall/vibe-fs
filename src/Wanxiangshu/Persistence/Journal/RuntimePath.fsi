namespace Wanxiangshu.Persistence.Journal

module RuntimePath =
    /// 主工作树的本机状态目录；不读取或迁入旧 Git 数据。
    val stateRoot: workspace: string -> string
    val initializeStateRoot: workspace: string -> string
    val forWorkspace: workspace: string -> string
    val forStateRoot: stateDirectory: string -> string
