// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
namespace Microsoft.AspNetCore.Routing.FunctionalTests;

public class AntiforgeryTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    [Fact]
    public async Task MapPost_WithForm_ValidToken_Works()
    {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAntiforgery();
                        app.UseEndpoints(b =>
                            b.MapPost("/todo", ([FromForm] Todo todo) => todo));
                    })
                    .UseTestServer();
            })
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAntiforgery();
            })
            .Build();

        using var server = host.GetTestServer();
        await host.StartAsync();
        var client = server.CreateClient();

        var antiforgery = host.Services.GetRequiredService<IAntiforgery>();
        var antiforgeryOptions = host.Services.GetRequiredService<IOptions<AntiforgeryOptions>>();
        var tokens = antiforgery.GetAndStoreTokens(new DefaultHttpContext());
        var request = new HttpRequestMessage(HttpMethod.Post, "todo");
        request.Headers.Add("Cookie", antiforgeryOptions.Value.Cookie.Name + "=" + tokens.CookieToken);
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string,string>("__RequestVerificationToken", tokens.RequestToken),
            new KeyValuePair<string,string>("name", "Test task"),
            new KeyValuePair<string,string>("isComplete", "false"),
            new KeyValuePair<string,string>("dueDate", DateTime.Today.AddDays(1).ToString()),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<Todo>(body, SerializerOptions);
        Assert.Equal("Test task", result.Name);
        Assert.False(result.IsCompleted);
        Assert.Equal(DateTime.Today.AddDays(1), result.DueDate);
    }

    [Fact]
    public async Task MapPost_WithForm_InvalidToken_Fails()
    {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAntiforgery();
                        app.UseEndpoints(b =>
                            b.MapPost("/todo", ([FromForm] Todo todo) => todo));
                    })
                    .UseTestServer();
            })
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAntiforgery();
            })
            .Build();

        using var server = host.GetTestServer();
        await host.StartAsync();
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "todo");
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string,string>("name", "Test task"),
            new KeyValuePair<string,string>("isComplete", "false"),
            new KeyValuePair<string,string>("dueDate", DateTime.Today.AddDays(1).ToString()),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MapPost_WithForm_WithoutMiddleware_ThrowsException()
    {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(b =>
                            b.MapPost("/todo", ([FromForm] Todo todo) => todo));
                    })
                    .UseTestServer();
            })
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAntiforgery();
            })
            .Build();

        using var server = host.GetTestServer();
        await host.StartAsync();
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "todo");
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string,string>("name", "Test task"),
            new KeyValuePair<string,string>("isComplete", "false"),
            new KeyValuePair<string,string>("dueDate", DateTime.Today.AddDays(1).ToString()),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.SendAsync(request));
        Assert.Equal(
            "Endpoint HTTP: POST /todo contains anti-forgery metadata, but a middleware was not found that supports anti-forgery.\nConfigure your application startup by adding app.UseAntiforgery() in the application startup code. If there are calls to app.UseRouting() and app.UseEndpoints(...), the call to app.UseAntiforgery() must go between them.",
            exception.Message);
    }

    [Fact]
    public async Task MapPost_WithForm_WithoutAntiforgery_WithoutMiddleware_Works()
    {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(b =>
                            b.MapPost("/todo", ([FromForm] Todo todo) => todo)
                            .DisableAntiforgery());
                    })
                    .UseTestServer();
            })
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAntiforgery();
            })
            .Build();

        using var server = host.GetTestServer();
        await host.StartAsync();
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "todo");
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string,string>("fasfafafafafafa", "Test task"),
            new KeyValuePair<string,string>("name", "Test task"),
            new KeyValuePair<string,string>("isComplete", "false"),
            new KeyValuePair<string,string>("dueDate", DateTime.Today.AddDays(1).ToString()),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<Todo>(body, SerializerOptions);
        Assert.Equal("Test task", result.Name);
        Assert.False(result.IsCompleted);
        Assert.Equal(DateTime.Today.AddDays(1), result.DueDate);
    }

    class Todo
    {
        public string Name { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime DueDate { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    class FromFormAttribute(string name = "") : Attribute, IFromFormMetadata
    {
        public string Name => name;
    }
}
