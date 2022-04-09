module DotEnv

open System
open System.IO

let private parseLine(line : string) =
  match line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries) with
  | args when args.Length = 2 ->
    Environment.SetEnvironmentVariable(args.[0], args.[1])
  | _ -> ()

let private load() =
  lazy (
    Console.WriteLine "Trying to load .env file..."
    let dir = Directory.GetCurrentDirectory()
    let filePath = Path.Combine(dir, ".env")
    filePath
    |> File.Exists
    |> function
      | false -> Console.WriteLine "No .env file found."
      | true  ->
        Console.WriteLine "Using .env file."
        filePath
        |> File.ReadAllLines
        |> Seq.iter parseLine
  )

let private init = load().Value

let getEnvironmentVariableOption key =
  init
  let result = Environment.GetEnvironmentVariable key
  if String.IsNullOrWhiteSpace(result) then None else result |> Some
  
let getEnvironmentVariable key =
  init
  Environment.GetEnvironmentVariable key