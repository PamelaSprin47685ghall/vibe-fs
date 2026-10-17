namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity

type private CompletedWorkRecordRepresentation =
    { Frontier: XTraceCursor
      FramesNewestFirst: BlogFrame list }

type CompletedWorkRecordProjectionState =
    private
        { ByProviderRun: Map<ProviderRunIdentity, CompletedWorkRecordRepresentation>
          FirstAtFrontier: Map<int64, ProviderRunIdentity> }

module CompletedWorkRecordProjection =
    let empty =
        { ByProviderRun = Map.empty
          FirstAtFrontier = Map.empty }

    let private addFirstAtFrontier sequence providerRun firstAtFrontier =
        if Map.containsKey sequence firstAtFrontier then
            firstAtFrontier
        else
            Map.add sequence providerRun firstAtFrontier

    let capture providerRun frontier framesNewestFirst state =
        if Map.containsKey providerRun state.ByProviderRun then
            state
        else
            let sequence = XTraceCursor.sequence frontier

            { ByProviderRun =
                Map.add
                    providerRun
                    { Frontier = frontier
                      FramesNewestFirst = framesNewestFirst }
                    state.ByProviderRun
              FirstAtFrontier = addFirstAtFrontier sequence providerRun state.FirstAtFrontier }

    let framesForRange providerRun endExclusive state =
        let sequence = XTraceCursor.sequence endExclusive

        providerRun
        |> Option.orElseWith (fun () -> Map.tryFind sequence state.FirstAtFrontier)
        |> Option.bind (fun run -> Map.tryFind run state.ByProviderRun)
        |> Option.filter (fun completed -> XTraceCursor.sequence completed.Frontier = sequence)
        |> Option.map (fun completed -> List.rev completed.FramesNewestFirst)
