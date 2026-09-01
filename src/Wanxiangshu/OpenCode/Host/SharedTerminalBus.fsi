namespace Wanxiangshu.OpenCode

module SharedTerminalBus =
    val acquire: directory: string -> Events.HostEventPort
    val release: directory: string option -> port: Events.HostEventPort option -> unit
    val tryAcquireForWorkspace: workspace: string option -> (string * Events.HostEventPort) option
