namespace Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module ProviderResourceBytes =

    let exists (relativePath: string) : bool = PackageResources.exists relativePath

    let readText (relativePath: string) : string = PackageResources.readText relativePath
