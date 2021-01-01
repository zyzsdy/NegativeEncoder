using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace NegativeEncoder.Utils
{
    public static class HttpClientFactory
    {
        private static IHttpClientFactory _factory;
        public static IHttpClientFactory ClientFactory
        {
            get
            {
                if(_factory == null)
                {
                    var serviceCollection = new ServiceCollection();
                    serviceCollection.AddHttpClient("default").ConfigurePrimaryHttpMessageHandler(h =>
                    {
                        return new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (message, cert, chain, error) => true
                        };
                    });

                    var serviceProvider = serviceCollection.BuildServiceProvider();
                    _factory = serviceProvider.GetService<IHttpClientFactory>();
                }

                return _factory;
            }
        }

        public static HttpClient GetHttpClient()
        {
            var client =  ClientFactory.CreateClient("default");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(SystemInfo.UA);
            return client;
        }
    }
}
