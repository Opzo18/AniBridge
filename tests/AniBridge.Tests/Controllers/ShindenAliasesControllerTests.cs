using System.Net;
using System.Text;
using AniBridge.Controllers;
using AniBridge.Providers.Shinden;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AniBridge.Tests.Controllers;

public class ShindenAliasesControllerTests
{
    private const string TitlePageHtml = """
        <html><head><title>Dogulwang</title></head><body>
        <h1 class="page-title">Dogulwang</h1>
        <div class="title-other parent-on-hover">
          <a href="/series/70344-dogulwang/titles_list">Edytuj tytuły</a>
          도굴왕, 盗掘王, Tomb Raider King
        </div>
        </body></html>
        """;

    [Fact]
    public async Task Get_ReturnsAliasesFromTitlePage()
    {
        var controller = new ShindenAliasesController(ListsService(TitlePageHtml));

        var result = await controller.Get("https://shinden.pl/series/70344-dogulwang", CancellationToken.None);

        var value = Assert.IsType<ShindenAliasesResult>(
            Assert.IsType<ActionResult<ShindenAliasesResult>>(result).Value);
        Assert.Equal(["도굴왕", "盗掘王", "Tomb Raider King"], value.Aliases);
    }

    [Fact]
    public async Task Get_EmptyUrl_ReturnsBadRequest()
    {
        var controller = new ShindenAliasesController(ListsService(TitlePageHtml));

        var result = await controller.Get("  ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_FailedFetch_ReturnsEmptyAliases()
    {
        var controller = new ShindenAliasesController(
            ListsService(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await controller.Get("https://shinden.pl/series/1-x", CancellationToken.None);

        var value = Assert.IsType<ShindenAliasesResult>(
            Assert.IsType<ActionResult<ShindenAliasesResult>>(result).Value);
        Assert.Empty(value.Aliases);
    }

    private static ShindenListService ListsService(string html) =>
        ListsService(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        });

    private static ShindenListService ListsService(HttpResponseMessage response)
    {
        var handler = new SingleResponseHandler(response);
        var http = new HttpClient(handler) { BaseAddress = new Uri(ShindenClient.BaseUrl + "/") };
        ShindenClient.ApplyBrowserHeaders(http);
        var client = new ShindenClient(http, NullLogger<ShindenClient>.Instance);
        return new ShindenListService(client, NullLogger<ShindenListService>.Instance);
    }

    private sealed class SingleResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
