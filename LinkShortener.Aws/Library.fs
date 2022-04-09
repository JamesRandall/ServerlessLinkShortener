module LinkShortener.Aws

open System
open System.Collections.Generic
open System.Net
open Amazon.DynamoDBv2
open Amazon.DynamoDBv2.DocumentModel
open Amazon.DynamoDBv2.Model

let private dynamoClient = new AmazonDynamoDBClient ()

let private documentFromList model =
  Document.FromAttributeMap(
    Dictionary(
      model
      |> Seq.map(fun row -> KeyValuePair(row |> fst, row |> snd)) 
    )
  )
  
let private isHttpSuccess (httpStatusCode:HttpStatusCode) =
  (httpStatusCode |> int) >= 200 && (httpStatusCode |> int) < 300 

let saveLink tableName (code:string) (url:string) = async {
  try
    let! response = 
      dynamoClient.PutItemAsync(
        PutItemRequest(
          tableName,
          Dictionary(
            ["shortcode",AttributeValue(code) ; "url",AttributeValue(url)]
            |> Seq.map(fun row -> KeyValuePair(row |> fst, row |> snd)) 
          ),
          ConditionExpression="attribute_not_exists(shortcode)"
        )
      ) |> Async.AwaitTask
    match response.HttpStatusCode |> isHttpSuccess with
    | true -> return Ok ()
    | false ->
      match response.HttpStatusCode with
      | HttpStatusCode.Conflict ->
        return Error PersistenceError.KeyConflict
      | _ -> return Error (PersistenceError.UnexpectedError $"Returned status code {response.HttpStatusCode}")
  with
  | :? AggregateException as exn ->
    return Error (
      exn.Flatten().InnerExceptions
      |> Seq.tryFind(function | :? ConditionalCheckFailedException -> true | _ -> false)
      |> Option.map(fun _ -> PersistenceError.KeyConflict)
      |> Option.defaultValue (PersistenceError.UnexpectedError exn.Message)
    )
  | exn -> return Error (PersistenceError.UnexpectedError $"Exception: {exn.GetType().Name}\n{exn.Message}")
}

let getLink tableName (code:string) = async {
  let! response =
    dynamoClient.GetItemAsync(
      GetItemRequest(
        tableName,
        Dictionary(
          ["shortcode",AttributeValue(code)]
          |> Seq.map(fun row -> KeyValuePair(row |> fst, row |> snd)) 
        )
      )
    ) |> Async.AwaitTask
  match response.HttpStatusCode |> isHttpSuccess with
  | true -> return GetResult.Link response.Item.["url"].S
  | false ->
    match response.HttpStatusCode with
    | HttpStatusCode.NoContent
    | HttpStatusCode.NotFound ->
      return GetResult.NotFound
    | _ ->
      return GetResult.UnexpectedError $"Error with HTTP status code {response.HttpStatusCode}"
}