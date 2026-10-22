namespace Wanxiangshu.Composition.Plugin

open Wanxiangshu.Host.Ports

type ProductHostPlugin =
    new: unit -> ProductHostPlugin
    interface IHostPlugin
