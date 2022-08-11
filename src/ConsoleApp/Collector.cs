using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using HtmlAgilityPack;

using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace ConsoleApp;

class Collector
{
    readonly MongoDBContext context;
    public Collector(MongoDBContext context)
    {
        this.context = context;
    }

    public async Task RunAsync()
    {
        await AddCountryAsync();

        var i = 0;
        while (i < 5)
        {
            Console.WriteLine();
            Console.WriteLine($"【{i}】GetChildrenAsync");
            await GetChildrenAsync(i);
            if (await context.DataSet.AsQueryable().Where(_ => _.Level == i).AnyAsync(_ => _.ChildrenLoaded == null))
            {
                continue;
            }
            else
            {
                Console.WriteLine($"【{i}】GetChildrenAsync complete!\t{await context.DataSet.AsQueryable().CountAsync(_ => _.Level == i + 1)}");

                i++;
            }
        }

        Console.WriteLine($"All jobs done!\t{await context.DataSet.AsQueryable().CountAsync()}");
    }

    async Task AddCountryAsync()
    {
        if (await context.DataSet.AsQueryable().AnyAsync(_ => _.Code == "0"))
            return;

        await context.DataSet.InsertOneAsync(new AdministrativeDivisionCn
        {
            Code = "0",
            Level = 0,
            Display = "中华人民共和国",
            ChildrenUrl = "http://www.stats.gov.cn/tjsj/tjbz/tjyqhdmhcxhfdm/2021/index.html",
        });
    }

    async Task GetChildrenAsync(int level)
    {
        var parents = await context.DataSet.AsQueryable()
            .Where(_ => _.Level == level)
            .OrderBy(_ => _.Code)
            .ToListAsync();
        foreach (var parent in parents)
        {
            Console.WriteLine();
            Console.WriteLine(parent);

            if (parent.ChildrenLoaded.HasValue)
                continue;

            if (string.IsNullOrEmpty(parent.ChildrenUrl))
            {
                parent.ChildrenLoaded = false;
                await context.DataSet
                    .UpdateOneAsync(_ => _.Code == parent.Code, Builders<AdministrativeDivisionCn>.Update
                    .Set(_ => _.ChildrenLoaded, parent.ChildrenLoaded));
                Console.WriteLine(parent);

                continue;
            }

            var (success, children) = await TryGetChildrenAsync(parent);
            if (success)
            {
                foreach (var item in children)
                {
                    if (await context.DataSet.AsQueryable().AnyAsync(_ => _.Code == item.Code))
                        continue;

                    await context.DataSet.InsertOneAsync(item);
                    Console.WriteLine($"\t{item}");
                }

                parent.ChildrenLoaded = true;
                await context.DataSet
                    .UpdateOneAsync(_ => _.Code == parent.Code, Builders<AdministrativeDivisionCn>.Update
                    .Set(_ => _.ChildrenLoaded, parent.ChildrenLoaded));
                Console.WriteLine(parent);
            }
        }
    }
    static async Task<(bool Success, IEnumerable<AdministrativeDivisionCn> Children)> TryGetChildrenAsync(AdministrativeDivisionCn parent)
    {
        static IEnumerable<(string Code, string Display, string Href)> GetProvinces(HtmlDocument html)
        {
            return html.DocumentNode.Descendants("tr").Where(_ => _.HasClass("provincetr"))
                .SelectMany(_ => _.Descendants("a"))
                .Select(_ =>
                {
                    var href = _.GetAttributeValue("href", string.Empty);
                    var code = href.Substring(0, href.IndexOf('.'));
                    var display = _.InnerText;
                    return (code[0..2], display, href);
                });
        }
        static IEnumerable<(string Code, string Display, string Href)> GetCities(HtmlDocument html)
        {
            return html.DocumentNode.Descendants("tr").Where(_ => _.HasClass("citytr"))
                .Select(_ =>
                {
                    var href = _.Descendants("a").FirstOrDefault()?.GetAttributeValue("href", string.Empty);
                    var code = _.Descendants("td").FirstOrDefault().InnerText;
                    var display = _.Descendants("td").LastOrDefault().InnerText;
                    return (code[0..4], display, href);
                });
        }
        static IEnumerable<(string Code, string Display, string Href)> GetCounties(HtmlDocument html)
        {
            return html.DocumentNode.Descendants("tr").Where(_ => _.HasClass("countytr"))
                .Select(_ =>
                {
                    var href = _.Descendants("a").FirstOrDefault()?.GetAttributeValue("href", string.Empty);
                    var code = _.Descendants("td").FirstOrDefault().InnerText;
                    var display = _.Descendants("td").LastOrDefault().InnerText;
                    return (code[0..6], display, href);
                });
        }
        static IEnumerable<(string Code, string Display, string Href)> GetTowns(HtmlDocument html)
        {
            return html.DocumentNode.Descendants("tr").Where(_ => _.HasClass("towntr"))
                .Select(_ =>
                {
                    var href = _.Descendants("a").FirstOrDefault()?.GetAttributeValue("href", string.Empty);
                    var code = _.Descendants("td").FirstOrDefault().InnerText;
                    var display = _.Descendants("td").LastOrDefault().InnerText;
                    return (code[0..9], display, href);
                });
        }
        static IEnumerable<(string Code, string Display, string Href)> GetVillages(HtmlDocument html)
        {
            return html.DocumentNode.Descendants("tr").Where(_ => _.HasClass("villagetr"))
                .Select(_ =>
                {
                    var code = _.Descendants("td").FirstOrDefault().InnerText;
                    var display = _.Descendants("td").LastOrDefault().InnerText;
                    return (code, display, default(string));
                });
        }
        Func<HtmlDocument, IEnumerable<(string Code, string Display, string Href)>> func = parent.Level switch
        {
            0 => GetProvinces,
            1 => GetCities,
            2 => ("4419" == parent.Code || "4420" == parent.Code || "4604" == parent.Code) ? GetTowns : GetCounties,
            3 => GetTowns,
            4 => GetVillages,
            _ => default,
        };
        var (success, html) = await TryLoadHtmlAsync(parent.ChildrenUrl);
        if (success)
        {
            var baseUrl = parent.ChildrenUrl.Substring(0, parent.ChildrenUrl.LastIndexOf('/'));
            var children = func(html).Select(_ => new AdministrativeDivisionCn
            {
                Code = _.Code,
                Level = GetLevel(_.Code),
                Display = _.Display,
                ChildrenUrl = string.IsNullOrEmpty(_.Href) ? null : $"{baseUrl}/{_.Href}",
            });
            return (true, children);
        }
        else
        {
            return (false, null);
        }
    }
    static async Task<(bool Success, HtmlDocument HtmlDoc)> TryLoadHtmlAsync(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            request.Headers.Add("Accept-Encoding", "deflate");
            request.Headers.Add("Referrer", "http://www.stats.gov.cn");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.212 Safari/537.36 Edg/90.0.818.62");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.Load(await response.Content.ReadAsStreamAsync(), Encoding.UTF8);
                return (true, htmlDoc);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"【请求错误】：{e.Message}，链接地址：{url}");
        }

        return (false, null);
    }
    static int GetLevel(string code)
    {
        return code switch
        {
            "0" => 0,
            _ when code.Length == 2 => 1,
            _ when code.Length == 4 => 2,
            _ when code.Length == 6 => 3,
            _ when code.Length == 9 => 4,
            _ when code.Length == 12 => 5,
            _ => -1,
        };
    }
}
