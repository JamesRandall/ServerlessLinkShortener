module Main

open System
open LinkShortener
open LinkShortener.Aws
let tableName = "gfoghlinkshortener_dev"
let domainName = "shortdomain.com"


[<EntryPoint>]
let main args =
  let defaultColor = Console.ForegroundColor
  let output color (message:string) =
    Console.ForegroundColor <- color
    Console.WriteLine message
    Console.ForegroundColor <- defaultColor
  let success = output ConsoleColor.Green
  let error = output ConsoleColor.Red
  
  // just grubby, to checking
  let get, save = initLinkShortener (getLink tableName) (saveLink tableName) 5 domainName
  let shortlinkResult = save args.[0] |> Async.RunSynchronously
  match shortlinkResult with
  | Ok shortlink ->
    success $"Short link generated {shortlink}"
    let shortcode = shortlink.Split("/") |> Array.last
    let retrievedLinkResult = get shortcode |> Async.RunSynchronously
    match retrievedLinkResult with
    | GetResult.Link link ->
      success $"Retrieved link {link}"
      0
    | GetResult.NotFound ->
      error "Not found"
      0
    | GetResult.UnexpectedError e ->
      error $"Unexpected get error: {e}"
      -1
  | Error e ->
    error $"Unexpected save error: {e}"
    -1
    
    
    