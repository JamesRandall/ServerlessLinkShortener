cd ../LinkShortener.Lambda
dotnet tool install -g Amazon.Lambda.Tools
dotnet lambda package -o output.zip
cd ../LinkShortener.Deploy
cdk deploy LinkShortenerDeployStack --parameters domainName=$1 --parameters tableName=$2 --parameters hostedZoneId=$3
rm ../LinkShortener.Lambda/output.zip
