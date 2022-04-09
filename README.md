# Super simple serverless link shortener

This is an insanely simple link shortener that makes use of Lambda and DynamoDB to keep the cost low.

Mostly posting as I thought it might be a useful example of some AWS stuff for folk.

_Full disclaimer:_ This really isn't designed to be used as some kind of high throughput super scale link shortener, it just did a simple job for me in a specific scenario. Codes are generated randomly and there is no attempt to dedupe links.

## Deployment

You will need CDK and its prerequisites installed to deploy the system and also .NET 6. You will also need a domain name registered in Route 53.

With those prerequisites in place you first need to create a new secret in AWS Secrets Manager. This is the API key that will be used to protect the POST verb that associates a shortcode with a link. The API key needs to be called:

    linkshortener/apikey

Set the secret as the plain text form, delete the JSON in the edit box, and simply set the API key you want to use.

Now navigate to the LinkShortener.Deploy folder and run the command:

    ./deploy.sh {domainName} {tableName} {hostedZoneId}

For example:

    ./deploy.sh mydomain.com mydynamotable Z000JDHDHDWUU$

You can find your hosted zone ID in the Route 53 list of hosted zones.

If all is well that will build the lambdas and deploy to AWS.

If you don't want to use an API key then uncomment the two marked lines in the CDK stack.

## Usage

To save a new link POST the link to https://yourdomain.com and if you are using an API key set it in the x-api-key header. This will return the short link in the response.

To use a short link simply paste it into your browser.

## Notes

You can actually get the CDK to build your .NET Lambda project however the image was failing when I tried this, not sure why, so for now this builds and packages the zip from a bash script.