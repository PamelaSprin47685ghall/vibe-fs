namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

module CasebookBookkeeperRefreshSurface =

    val refreshStale: store: obj -> root: string -> sessionId: string -> Task<obj>
