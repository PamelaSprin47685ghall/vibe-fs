namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks

module AttachmentSurface =
    val classifyObservation: observation: string -> obj

    val scenario:
        owner: string -> role: string -> firstAgent: string -> secondAgent: string -> usable: bool -> Task<obj>

    val reconciliationScenario: observation: string -> Task<obj>
