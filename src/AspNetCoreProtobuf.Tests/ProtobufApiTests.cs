﻿using AspNetCoreProtobuf.Model;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCoreProtobuf.Tests
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class ProtobufApiTests
    {
        private readonly HttpClient _client;
        private readonly ApiTokenInMemoryClient  _tokenService;
        private readonly IConfigurationRoot _configurationRoot;
        public ProtobufApiTests()
        {
            _configurationRoot = GetIConfigurationRoot();
            _client = new HttpClient
            {
                BaseAddress = new System.Uri(_configurationRoot["ApiUrl"])
            };

            _tokenService = new ApiTokenInMemoryClient(_configurationRoot["StsUrl"], new HttpClient());
        }

        private async Task SetTokenAsync(HttpClient client)
        {
            var access_token = await _tokenService.GetApiToken(
                    "ClientProtectedApi",
                    "apiproto",
                    _configurationRoot["ApiSecret"]
                );

            client.SetBearerToken(access_token);
        }

        [Fact]
        public async Task GetProtobufDataAsString()
        {
            await SetTokenAsync(_client);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-protobuf"));
            var response = await _client.GetAsync("/api/values/1");
            response.EnsureSuccessStatusCode();

            var responseString = System.Text.Encoding.UTF8.GetString(
                await response.Content.ReadAsByteArrayAsync()
            );

            // Assert
            Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType.MediaType);
            Assert.Equal("\b\u0001\u0012\nHelloWorld\u001a\u001fMy first MVC 6 Protobuf service", responseString);
        }

        [Fact]
        public async Task GetProtobufDataAndCheckProtobufContentTypeMediaType()
        {
            await SetTokenAsync(_client);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-protobuf"));
            var response = await _client.GetAsync("/api/values/1");
            response.EnsureSuccessStatusCode();

            var result = ProtoBuf.Serializer.Deserialize<ProtobufModelDto>(await response.Content.ReadAsStreamAsync());

            // Assert
            Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType.MediaType );
            Assert.Equal("My first MVC 6 Protobuf service", result.StringValue);
        }

        [Fact]
        public async Task PostProtobufDataAsync()
        {
            await SetTokenAsync(_client);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-protobuf"));

            MemoryStream stream = new MemoryStream();
            ProtoBuf.Serializer.Serialize<ProtobufModelDto>(stream, new ProtobufModelDto
            {
                Id = 2,
                Name = "lovely data",
                StringValue = "amazing this ah"

            });

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/Values")
            {
                Content = new StringContent(
                    StreamToString(stream),
                    Encoding.UTF8,
                    "application/x-protobuf")//CONTENT-TYPE header
            };

            var responseForPost = await _client.SendAsync(request);

            Assert.True(responseForPost.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Get401ForNoToken()
        {
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-protobuf"));
            var response = await _client.GetAsync("/api/values/1");

            // Assert
            Assert.Equal("Unauthorized", response.StatusCode.ToString());
        }

        [Fact]
        public async Task Get401ForIncorrectToken()
        {
            var access_token = await _tokenService.GetApiToken(
                    "ClientProtectedApi",
                    "dummy",
                    "apiprotoSecret"
                );

            _client.SetBearerToken(access_token);

            // Act
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-protobuf"));
            var response = await _client.GetAsync("/api/values/1");

            // Assert
            Assert.Equal("Unauthorized", response.StatusCode.ToString());
        }

        private static string StreamToString(Stream stream)
        {
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static IConfigurationRoot GetIConfigurationRoot()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }
    }
}
