namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

/// HNI P-08: the OpenCode adapter for the neutral model catalog.
///
/// The adapter enumerates the routes the host advertises and translates the
/// host catalog into neutral `ModelRoute` values. It chooses no route:
/// choosing is product logic (`Interaction/Routing`).
///
/// The host applies a model *per prompt*, never per session, so `Apply` is
/// reported as `HostError.Unsupported HostCapability.ProviderParams` rather
/// than faked. Routing therefore happens at prompt time (HNI P-02
/// `PromptRequest.Route`).
module ModelCatalogPort =

    [<Emit("Array.isArray($0)")>]
    let private isJsArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isJsString (value: obj) : bool = jsNative

    [<Emit("fetch($0, $1)")>]
    let private jsFetch (url: string) (init: obj) : Task<obj> = jsNative

    /// A host value is absent when it is `null` or `undefined`; a missing
    /// host property reads as `undefined`.
    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    /// A host object field; a missing field reads as `undefined`.
    [<Emit("$0 == null ? undefined : $0[$1]")>]
    let private field (value: obj) (name: string) : obj = jsNative

    [<Emit("encodeURIComponent($0)")>]
    let private encodeUriComponent (value: string) : string = jsNative

    /// The text of a host string value; a non-string or absent value is
    /// reported as absent rather than coerced.
    let private readText (value: obj) : string option =
        if isNullish value || not (isJsString value) then
            None
        elif System.String.IsNullOrWhiteSpace(unbox<string> value) then
            None
        else
            Some(unbox<string> value)

    /// The OpenCode SDK encodes this header itself (see
    /// packages/sdk/js/src/client.ts). Raw Unicode paths (e.g. 万象) are
    /// rejected by fetch Headers as invalid.
    let private headersObj (workspaceDirectory: string option) : obj =
        match workspaceDirectory with
        | Some dir -> createObj [ "x-opencode-directory", box (encodeUriComponent dir) ]
        | None -> createObj []

    /// The directory query the catalog request carries.
    let private queryObj (workspaceDirectory: string option) : obj =
        match workspaceDirectory with
        | Some dir -> createObj [ "directory", box dir ]
        | None -> createObj []

    /// The host catalog payload, unwrapped from the SDK response envelope
    /// (`{ data, error, request, response }`).
    let private payloadOf (response: obj) : obj =
        if isNullish response then
            null
        elif
            not (isNullish (field response "data"))
            && not (isNullish (field (field response "data") "data"))
        then
            field (field response "data") "data"
        elif not (isNullish (field response "data")) then
            field response "data"
        else
            response

    /// The failure a catalog response carries, if any.
    let private failureOf (response: obj) : HostError option =
        if isNullish response then
            Some(HostError.Transport "The host returned no model catalog response")
        elif not (isNullish (field response "error")) then
            Some(HostError.Refused(sprintf "The host refused the model catalog request: %A" (field response "error")))
        else
            None

    /// The providers of a catalog payload. An absent provider list is an
    /// empty catalog, never a failure.
    let private providersOf (payload: obj) : obj array =
        let providers =
            if isNullish payload then
                null
            else
                field payload "providers"

        if isNullish providers then [||]
        elif isJsArray providers then unbox<obj array> providers
        else [||]

    /// The model ids one provider advertises. The catalog keys its models by
    /// the model id; a provider without a model map advertises none.
    let private modelKeysOf (provider: obj) : string array =
        let models = if isNullish provider then null else provider?models

        if isNullish models then
            [||]
        else
            emitJsExpr models "Object.keys($0)"

    /// The routes one provider advertises. The SDK catalog carries no variant
    /// field, so every enumerated route has `Variant = None`.
    let private routesOfProvider (provider: obj) : ModelRoute list =
        let providerId =
            if isNullish provider then
                None
            else
                readText (field provider "id")

        match providerId with
        | None -> []
        | Some id ->
            modelKeysOf provider
            |> Array.toList
            |> List.map (fun key ->
                { Provider = id
                  Model = key
                  Variant = None })

    /// The routes of a whole catalog payload.
    let private routesOfCatalog (payload: obj) : ModelRoute list =
        providersOf payload |> Array.toList |> List.collect routesOfProvider

    /// The catalog outcome carried by a host response.
    let private catalogOf (response: obj) : Result<ModelRoute list, HostError> =
        match failureOf response with
        | Some error -> Error error
        | None -> Ok(routesOfCatalog (payloadOf response))

    /// The catalog read over the OpenCode SDK client. The host call is
    /// `client.config.providers` (`GET /config/providers`), the only host
    /// verb that lists every provider together with its models.
    let private sdkAvailable
        (client: obj)
        (workspaceDirectory: string option)
        : Task<Result<ModelRoute list, HostError>> =
        task {
            let configObj = field client "config"
            let providersFn = field configObj "providers"

            if isNullish providersFn then
                return Error(HostError.Refused "The host does not expose a model catalog")
            else
                let payload =
                    createObj
                        [ "query", box (queryObj workspaceDirectory)
                          "headers", box (headersObj workspaceDirectory) ]

                let! response = unbox<Task<obj>> (providersFn?call (configObj, payload))
                return catalogOf response
        }

    /// The catalog read over the OpenCode HTTP server (`GET /config/providers`).
    let private httpAvailable
        (baseUrl: string)
        (workspaceDirectory: string option)
        : Task<Result<ModelRoute list, HostError>> =
        let cleanBaseUrl =
            if baseUrl.EndsWith("/") then
                baseUrl.Substring(0, baseUrl.Length - 1)
            else
                baseUrl

        let catalogUrl =
            match workspaceDirectory with
            | Some dir -> sprintf "%s/config/providers?directory=%s" cleanBaseUrl (encodeUriComponent dir)
            | None -> cleanBaseUrl + "/config/providers"

        task {
            let init =
                {| method = "GET"
                   headers = headersObj workspaceDirectory |}

            let! response = jsFetch catalogUrl init
            let status = unbox<int> response?status

            if status < 200 || status >= 300 then
                return Error(HostError.Transport(sprintf "The host answered the model catalog with HTTP %d" status))
            else
                let! text = unbox<Task<string>> (response?text ())
                let payload = Fable.Core.JS.JSON.parse text
                return Ok(routesOfCatalog payload)
        }

    /// The catalog over the OpenCode SDK client.
    type SdkModelCatalog(client: obj, workspaceDirectory: string option) =
        interface IModelCatalog with
            member this.Available() : Task<Result<ModelRoute list, HostError>> =
                task {
                    try
                        return! sdkAvailable client workspaceDirectory
                    with ex ->
                        return Error(HostError.Transport(sprintf "The model catalog request failed: %s" ex.Message))
                }

            /// The host binds a model to a *prompt*, never to a session: the
            /// session update verb carries only a title, and the model rides
            /// on the prompt payload (see `OpenCodePort.fs`
            /// `modelRequestFields`) or on a per-message projection. There is
            /// no session-level model to apply, so the port reports the
            /// capability as unsupported instead of inventing a write the host
            /// would ignore.
            member this.Apply(_session: SessionId, _route: ModelRoute) : Task<Result<unit, HostError>> =
                task { return Error(HostError.Unsupported HostCapability.ProviderParams) }

    /// The catalog over the OpenCode HTTP server.
    type HttpModelCatalog(baseUrl: string, workspaceDirectory: string option) =
        interface IModelCatalog with
            member this.Available() : Task<Result<ModelRoute list, HostError>> =
                task {
                    try
                        return! httpAvailable baseUrl workspaceDirectory
                    with ex ->
                        return Error(HostError.Transport(sprintf "The model catalog request failed: %s" ex.Message))
                }

            member this.Apply(_session: SessionId, _route: ModelRoute) : Task<Result<unit, HostError>> =
                task { return Error(HostError.Unsupported HostCapability.ProviderParams) }

    /// The catalog for the given host wiring, or `None` when the input names
    /// no host the catalog can be built on.
    let create (workspaceDirectory: string option) (input: obj) : IModelCatalog option =

        let client = if isNullish input then null else field input "client"

        if isNullish input then
            None
        elif not (isNullish client) && not (isNullish (field client "config")) then
            Some(SdkModelCatalog(client, workspaceDirectory) :> IModelCatalog)
        elif not (isNullish (field input "serverUrl")) then
            Some(HttpModelCatalog(unbox<string> (field input "serverUrl"), workspaceDirectory) :> IModelCatalog)
        elif not (isNullish (field input "baseUrl")) then
            Some(HttpModelCatalog(unbox<string> (field input "baseUrl"), workspaceDirectory) :> IModelCatalog)
        elif not (isNullish (field input "port")) then
            Some(
                HttpModelCatalog(sprintf "http://127.0.0.1:%d" (unbox<int> (field input "port")), workspaceDirectory)
                :> IModelCatalog
            )
        else
            None
