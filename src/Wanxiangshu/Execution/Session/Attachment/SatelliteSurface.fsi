namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks

module SatelliteSurface =
    val scenario: linked: bool -> physical: bool -> conflict: bool -> queryError: bool -> Task<obj>
    val concurrent: unit -> Task<obj>
