module Responses

open System.Collections.Generic
open System.Net
open Amazon.Lambda.APIGatewayEvents

let unauthorised () =
  APIGatewayProxyResponse(
    StatusCode = (HttpStatusCode.Unauthorized |> int),
    Body = "Unauthorised"
  )
  
let noContent () =
  APIGatewayProxyResponse(
    StatusCode = (HttpStatusCode.NoContent |> int)
  )
  
let badRequest msg =
  APIGatewayProxyResponse(
    StatusCode = (HttpStatusCode.BadRequest |> int),
    Body = msg
  )
  
let success content =
  APIGatewayProxyResponse(
    StatusCode = (HttpStatusCode.OK |> int),
    Body = content
  )
  
let redirect redirectTo =
  APIGatewayProxyResponse(
    StatusCode = (HttpStatusCode.Redirect |> int),
    Headers = Dictionary([KeyValuePair("Location",redirectTo)])
  )
  
let internalError error =
  APIGatewayProxyResponse(
    StatusCode = (HttpStatusCode.InternalServerError |> int),
    Body = error
  )
  
let fromAsyncResult asyncResult = async {
  match! asyncResult with
  | Ok content -> return success content
  | Error error -> return error
}

