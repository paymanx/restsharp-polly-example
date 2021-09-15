using Polly;
using RestSharp;
using System;
using System.Net;
using System.Threading.Tasks;

namespace RestSharpWithPolly
{
    public class RestSharpPollySample
    {
        public static int RetryAttempts { get; set; } = 5;
        public static TimeSpan PauseBetweenFailures { get; set; } = TimeSpan.FromSeconds(30);

        private IRestResponse RestResponseWithPolicy(RestClient restClient, RestRequest restRequest, Func<string, Task> logFunction)
        {
            var retryPolicy = Policy
                .HandleResult<IRestResponse>(x => !x.IsSuccessful)
                .WaitAndRetry(RetryAttempts, x => PauseBetweenFailures, async (iRestResponse, timeSpan, retryCount, context) =>
                {
                    await logFunction($"Request failed. HttpStatusCode={iRestResponse.Result.StatusCode}. Waiting {timeSpan} seconds before retry. Number attempt {retryCount}. Uri={iRestResponse.Result.ResponseUri}; RequestResponse={iRestResponse.Result.Content}");
                });

            var circuitBreakerPolicy = Policy
                .HandleResult<IRestResponse>(x => x.StatusCode == HttpStatusCode.ServiceUnavailable)
                .CircuitBreaker(1, TimeSpan.FromSeconds(60), onBreak: async (iRestResponse, timespan, context) =>
                {
                    await logFunction($"Circuit went into a fault state. Reason: {iRestResponse.Result.Content}");
                },
                onReset: async (context) =>
                {
                    await logFunction($"Circuit left the fault state.");
                });

            return retryPolicy.Wrap(circuitBreakerPolicy).Execute(() => restClient.Execute(restRequest));
        }
    }
}
