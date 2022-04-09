open Amazon.CDK
open LinkShortenerDeploy

[<EntryPoint>]
let main _ =
    let app = App(null)

    LinkShortenerDeployStack(app, "LinkShortenerDeployStack", StackProps()) |> ignore

    app.Synth() |> ignore
    0
