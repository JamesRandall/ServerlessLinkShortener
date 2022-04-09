namespace LinkShortenerDeploy

open Amazon.CDK
open Amazon.CDK.AWS
open Amazon.CDK.AWS.APIGateway
open Amazon.CDK.AWS.CertificateManager
open Amazon.CDK.AWS.DynamoDB
open Amazon.CDK.AWS.Lambda
open Amazon.CDK.AWS.Route53
open Amazon.CDK.AWS.Route53.Targets

type LinkShortenerDeployStack(scope, id, props) as this =
    inherit Stack(scope, id, props)
    let tableName = CfnParameter(this, "tableName")
    let domainName = CfnParameter(this, "domainName")
    let hostedZoneId = CfnParameter(this, "hostedZoneId")

    let dynamoTable =
      Table(
        this,
        "linkshortenertable",
        TableProps(
          TableName = tableName.ValueAsString,
          PartitionKey = Attribute(
            Name="shortcode",
            Type = AttributeType.STRING
          )
        )
      )
      
    let saveLambdaRole =
      IAM.Role(
        this,
        "linkshortenersaverole",
        IAM.RoleProps(
          AssumedBy = IAM.ServicePrincipal("lambda.amazonaws.com"),
          ManagedPolicies = [|
            IAM.ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            IAM.ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole")
            IAM.ManagedPolicy.FromAwsManagedPolicyName("AmazonS3FullAccess")
          |]
        )
      )
    do dynamoTable.GrantReadWriteData(saveLambdaRole) |> ignore
    // comment the below line out if you don't want to use an API key
    do SecretsManager.Secret.FromSecretNameV2(this, $"linkshortenerapikey", "linkshortener/apikey").GrantRead saveLambdaRole |> ignore
    let getLambdaRole =
      IAM.Role(
        this,
        "linkshortenergetrole",
        IAM.RoleProps(
          AssumedBy = IAM.ServicePrincipal("lambda.amazonaws.com"),
          ManagedPolicies = [|
            IAM.ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            IAM.ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole")
            IAM.ManagedPolicy.FromAwsManagedPolicyName("AmazonS3FullAccess")
          |]
        )
      )
    do dynamoTable.GrantReadData(getLambdaRole) |> ignore
    
    let getLambda = Function(
      this,
      "linkshortener-get-lambda-function",
      FunctionProps(
        Runtime = Runtime.DOTNET_6,
        Code = Code.FromAsset("../LinkShortener.Lambda/output.zip"),
        Handler = "LinkShortener.Lambda::LinkShortener.Lambda.LinkShortenerFunctions::GetLinkHandlerAsync",
        Environment = ([
          "tablename", tableName.ValueAsString
          "shortcodelength" , "5"
        ] |> Map.ofList),
        Role = getLambdaRole,
        Timeout = Duration.Seconds(10.),
        MemorySize = 2048. // lots of memory to get a decent CPU size - needed to get a decent .NET cold start time
      )  
    )
    let saveLambda = Function(
      this,
      "linkshortener-save-lambda-function",
      FunctionProps(
        Runtime = Runtime.DOTNET_6,
        Code = Code.FromAsset("../LinkShortener.Lambda/output.zip"),
        Handler = "LinkShortener.Lambda::LinkShortener.Lambda.LinkShortenerFunctions::SaveLinkHandlerAsync",
        Environment = ([
          "tablename", tableName.ValueAsString
          "shortcodelength" , "5"
          // comment the below line out if you don't want to use an API key
          "useapikey", "true"
        ] |> Map.ofList),
        Role = saveLambdaRole,
        Timeout = Duration.Seconds(10.),
        MemorySize = 2048. // lots of memory to get a decent CPU size - needed to get a decent .NET cold start time
      )  
    )
    
    let domainZone =
      Route53.HostedZone.FromHostedZoneAttributes(
        this,
        "hostedzone",
        HostedZoneAttributes(
          HostedZoneId = hostedZoneId.ValueAsString,
          ZoneName = domainName.ValueAsString
        )
      )
    let certificate =
      Certificate(
        this,
        "linkshortenercert",
        CertificateProps(
          DomainName = domainName.ValueAsString,
          Validation = CertificateValidation.FromDns(domainZone)
        )
      )
    
    let api = RestApi(
      this,
      "linkshortener-api",
      RestApiProps(
        DomainName = DomainNameOptions(
          Certificate = certificate,
          DomainName = domainName.ValueAsString
        )
      )
    )
    let getWidgetsIntegration =
      LambdaIntegration(
        getLambda,
        LambdaIntegrationOptions(
          RequestTemplates = ([
            "application/json","{ \"statusCode\": \"200\" }"
          ] |> Map.ofList)
        )
      )
    let parameterisedPath = api.Root.AddResource("{shortcode}")
    do parameterisedPath.AddMethod("GET", getWidgetsIntegration) |> ignore
    let saveWidgetsIntegration =
      LambdaIntegration(
        saveLambda,
        LambdaIntegrationOptions(
          RequestTemplates = ([
            "application/json","{ \"statusCode\": \"200\" }"
          ] |> Map.ofList)
        )
      )
    do api.Root.AddMethod("POST", saveWidgetsIntegration) |> ignore
    
    do
      Route53.ARecord(
        this,
        "linkshortener-arecord",
        ARecordProps(
          Zone = domainZone,
          Target = Route53.RecordTarget.FromAlias(Route53.Targets.ApiGateway(api))
        )
      )
      |> ignore
