using System.Net;
using System.Threading.Tasks;
using NegativeEncoder.Utils;
using Newtonsoft.Json.Linq;

namespace NegativeEncoder.About;

public class CheckUpdate
{
    public static async Task Check()
    {
        AppContext.Status.MainStatus = "检查更新...";

        string responseStr;
        try
        {
            var githubApiUrl = "https://api.github.com/repos/zyzsdy/NegativeEncoder/releases";
            var httpClient = HttpClientFactory.GetHttpClient();

            var response = await httpClient.GetAsync(githubApiUrl);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                AppContext.Status.MainStatus = "检查更新失败";
                return;
            }

            responseStr = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(responseStr))
            {
                AppContext.Status.MainStatus = "检查更新失败";
                return;
            }
        }
        catch
        {
            AppContext.Status.MainStatus = "检查更新：网络连接失败";
            return;
        }

        string tag;
        string url;
        try
        {
            var releaseObj = JArray.Parse(responseStr);
            var releaseNote = releaseObj[0];
            tag = releaseNote["tag_name"].ToString();
            url = releaseNote["html_url"].ToString();
        }
        catch
        {
            AppContext.Status.MainStatus = "检查更新：版本信息解析失败";
            return;
        }

        var nowVersionTag = $"v{AppContext.Version.CurrentVersion}";
        if (nowVersionTag == tag)
        {
            AppContext.Version.IsLatest = true;
            AppContext.Status.MainStatus = "当前已是最新版本";
            return;
        }

        AppContext.Version.LatestVersion = tag;
        AppContext.Version.UpdateVersionLinkUrl = url;
        AppContext.Version.IsLatest = false;

        AppContext.Status.MainStatus = $"发现新版本 {tag}，请使用「帮助」-「新版本 {tag}」更新。";
    }
}