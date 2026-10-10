using System.Reflection;
using WhaleDeck.Application.Models;
using WhaleDeck.Infrastructure.Catalog;

namespace WhaleDeck.IntegrationTests.Catalog;

public sealed class XuanyuanCatalogProviderTests
{
    [Fact]
    public void PopularPageParserRecognizesCurrentLocalizedRepositoryLinks()
    {
        const string html = """
            <h3>第1名：<a href="/zh/r/vllm/vllm-openai">vllm/vllm-openai</a></h3>
            <h3>第2名：<a href="/zh/r/library/mysql">library/mysql</a></h3>
            <h3>第3名：<a href="https://xuanyuan.cloud/zh/r/library/nginx">library/nginx</a></h3>
            """;
        var method = typeof(XuanyuanCatalogProvider).GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Catalog parser was not found.");

        var parsed = Assert.IsAssignableFrom<IEnumerable<CatalogApplicationDto>>(method.Invoke(null, [html])).ToArray();

        Assert.Equal(3, parsed.Length);
        Assert.Equal("vllm-vllm-openai", parsed[0].Id);
        Assert.Equal("vllm/vllm-openai", parsed[0].Image);
        Assert.Equal("library/mysql", parsed[1].Image);
        Assert.Equal("library/nginx", parsed[2].Image);
    }
}
