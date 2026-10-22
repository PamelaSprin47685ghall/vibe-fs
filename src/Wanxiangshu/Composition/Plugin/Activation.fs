namespace Wanxiangshu.Composition.Plugin

open System.Threading.Tasks
open Wanxiangshu.Host.Ports

type ProductHostPlugin() =
    interface IHostPlugin with
        member this.Activate(_ports: HostPorts) : HostResult<ProductHooks> =
            let hooks: ProductHooks =
                { ToolGate = None
                  Turn = None
                  Compaction = CompactionGate.create ()
                  Spawn = None
                  OnSignal = None
                  Dispose = fun () -> Task.FromResult() }

            Task.FromResult(Ok hooks)
