namespace Wanxiangshu.Composition.Plugin

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

module CompactionGate =

    type private DefaultCompactionGate() =
        interface ICompactionGate with
            member this.OnCompacting(_session: SessionId) : Task<CompactionContribution> =
                Task.FromResult CompactionContribution.Nothing

            member this.AutoContinue(_session: SessionId) : Task<ContinueVerdict> =
                let verdict =
                    match Wanxiangshu.Host.HostCompactionPolicy.autoContinueEnabled with
                    | true -> ContinueVerdict.Allow
                    | false ->
                        ContinueVerdict.Deny
                            "compaction.auto is false: the synthetic continuation is unreachable (HOST-006)"

                Task.FromResult verdict

    let create () : ICompactionGate =
        DefaultCompactionGate() :> ICompactionGate
