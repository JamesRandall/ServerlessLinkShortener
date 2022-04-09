namespace LinkShortener.Lambda
open Amazon.Lambda.APIGatewayEvents
open Amazon.Lambda.Core
open Amazon.SecretsManager
open Amazon.SecretsManager.Model
open LinkShortener

module AsyncResult =
  let bind handler result = async {
    match! result with
    | Ok value -> return! handler value
    | Error e -> return Error e
  }

[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()

type LinkShortenerFunctions() =
  member __.GetLinkHandlerAsync (event:APIGatewayProxyRequest) (context:ILambdaContext) = task {
    let tableName = DotEnv.getEnvironmentVariable "tablename"
    let shortcodeLength = DotEnv.getEnvironmentVariable "shortcodelength" |> int
    let domainName = event.RequestContext.DomainName
    let get, _ =
      initLinkShortener (Aws.getLink tableName) (Aws.saveLink tableName) shortcodeLength domainName
    return!
      match event.PathParameters.TryGetValue "shortcode" with
      | true, shortcode -> async {
          let! getResponse = get shortcode
          return
            match getResponse with
            | GetResult.Link link ->
              Responses.redirect link
            | GetResult.NotFound ->
              Responses.noContent ()
            | GetResult.UnexpectedError e ->
              context.Logger.LogError e
              Responses.internalError "Internal error"
        }
      | _ -> async { return Responses.badRequest "No shortcode" }
  }
  
  member __.SaveLinkHandlerAsync (event:APIGatewayProxyRequest) (context: ILambdaContext) = task {
    let tableName = DotEnv.getEnvironmentVariable "tablename"
    let shortcodeLength = DotEnv.getEnvironmentVariable "shortcodelength" |> int
    
    let authorised = async {
      return!
        match DotEnv.getEnvironmentVariableOption "useapikey" with
        | Some "true" -> async {
            use secretManagerClient = new AmazonSecretsManagerClient()
            let! secretResponse =
              secretManagerClient.GetSecretValueAsync(GetSecretValueRequest(SecretId = "linkshortener/apikey"))
              |> Async.AwaitTask
            match event.Headers.TryGetValue("x-api-key") with
            | true, apiKeyHeaderValue ->
              return
                if apiKeyHeaderValue = secretResponse.SecretString then Ok () else Error (Responses.unauthorised ())
            | _ -> return Error (Responses.unauthorised ())
          }
        | _ -> async { return Ok () }
    }
    
    return!
      authorised
      |> AsyncResult.bind (fun _ -> async {
        return
          if System.String.IsNullOrWhiteSpace event.Body then Error (Responses.badRequest "No url supplied") else Ok ()
      })
      |> AsyncResult.bind (fun _ -> async {
        let domainName = event.RequestContext.DomainName
        let _, save =
          initLinkShortener (Aws.getLink tableName) (Aws.saveLink tableName) shortcodeLength domainName
        let! saveResponse = save event.Body
        return
          match saveResponse with
          | Ok shortUrl -> Ok shortUrl
          | Error error ->
            match error with
            | PersistenceError.KeyConflict -> Error (Responses.internalError "Unable to generate unique shortcode")
            | PersistenceError.UnexpectedError e ->
              context.Logger.Log $"Error: {e}"
              Error (Responses.internalError "Unexpected error")
      })
      |> Responses.fromAsyncResult
  }
