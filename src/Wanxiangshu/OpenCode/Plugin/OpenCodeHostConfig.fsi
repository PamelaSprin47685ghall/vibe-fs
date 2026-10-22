namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host.Ports

type LiveHostConfig =
    new: config: obj -> LiveHostConfig
    /// Adopt the config object the host just delivered, in place.
    member Accept: nextConfig: obj -> unit
    interface IHostConfigPort
