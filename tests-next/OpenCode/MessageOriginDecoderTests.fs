namespace Wanxiangshu.Next.Tests.OpenCode

open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

module MessageOriginDecoderTests =

    [<Fact>]
    let ``Unclaimed user-shaped message is unknown not human`` () =
        let message: OpencodeUserMessage =
            { id = "physical-user"
              role = "user"
              sessionID = "s1"
              agent = None
              model = None
              parts = [ createObj [ "type", box "text"; "text", box "ordinary words" ] ] }

        Assert.Equal(UnknownOrigin, MessageOriginDecoder.decodeUserMessageOrigin message)

    [<Fact>]
    let ``Prompt key remains plugin generated until acceptance mapping`` () =
        let message: OpencodeUserMessage =
            { id = "physical-user"
              role = "user"
              sessionID = "s1"
              agent = None
              model = None
              parts =
                [ createObj
                      [ "type", box "text"
                        "text", box "\u200B"
                        "metadata", box (createObj [ "wanxiangshu_prompt_key", box "pk-1" ]) ] ] }

        Assert.Equal(PluginGenerated(PromptKeyRef.create "pk-1"), MessageOriginDecoder.decodeUserMessageOrigin message)
